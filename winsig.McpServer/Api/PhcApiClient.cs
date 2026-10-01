using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Options;
using winsig.McpServer.Configuration;
using winsig.McpServer.Models;

namespace winsig.McpServer.Api;

public sealed class PhcApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<PhcApiOptions> options,
    TimeProvider clock,
    ILogger<PhcApiClient> logger) : IPhcApiClient, IDisposable
{
    public const string HttpClientName = "phc-api";
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private PhcApiLoginResponse? _login;

    public Task<JsonElement> GetBalanceteAsync(int? ano, int? mes, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, AccountingPath("balancete", ano, mes), null, false, ct);

    public Task<JsonElement> GetFluxoCaixaAsync(int? ano, int? mes, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, AccountingPath("fluxo-caixa", ano, mes), null, false, ct);

    public Task<JsonElement> GetHealthAsync(CancellationToken ct = default) =>
        SendAsync(HttpMethod.Get, "health", null, true, ct);

    public Task<PhcEnvelope<List<DocumentoAbertoDto>>> GetEncomendasAsync(
        int? orderNumber, ComercialQuery filters, CancellationToken ct = default)
    {
        var query = CommercialQuery(filters);
        if (orderNumber.HasValue)
            query.Add("orderNumber=" + orderNumber.Value.ToString(CultureInfo.InvariantCulture));
        return SendEnvelopeAsync(HttpMethod.Get, WithQuery("api/comercial/encomendas", query), null,
            PhcJsonContext.Default.PhcEnvelopeListDocumentoAbertoDto, ct);
    }

    public Task<PhcEnvelope<List<EncomendaDetalheDto>>> GetEncomendasDetalheAsync(
        int[]? orderNumbers, ComercialQuery filters, CancellationToken ct = default)
    {
        var query = CommercialQuery(filters);
        if (orderNumbers is not null)
        {
            // OpenAPI query arrays use form/explode: repeat the key for each value.
            foreach (var number in orderNumbers)
                query.Add("orderNumbers=" + number.ToString(CultureInfo.InvariantCulture));
        }
        return SendEnvelopeAsync(HttpMethod.Get, WithQuery("api/comercial/encomendas/detalhe", query), null,
            PhcJsonContext.Default.PhcEnvelopeListEncomendaDetalheDto, ct);
    }

    public Task<PhcEnvelope<CriarFaturaResultadoDto>> CriarFaturaAsync(
        CriarFaturaPedidoDto request, CancellationToken ct = default)
    {
        ValidateInvoice(request);
        return SendEnvelopeAsync(HttpMethod.Post, "api/comercial/faturas/de-encomendas",
            JsonContent.Create(request, PhcJsonContext.Default.CriarFaturaPedidoDto),
            PhcJsonContext.Default.PhcEnvelopeCriarFaturaResultadoDto, ct);
    }

    public Task<PhcEnvelope<List<UtilizadorDto>>> GetUtilizadoresAsync(CancellationToken ct = default) =>
        SendEnvelopeAsync(HttpMethod.Get, "api/utilizadores", null,
            PhcJsonContext.Default.PhcEnvelopeListUtilizadorDto, ct);

    private async Task<PhcEnvelope<T>> SendEnvelopeAsync<T>(
        HttpMethod method, string path, HttpContent? payload,
        JsonTypeInfo<PhcEnvelope<T>> typeInfo, CancellationToken ct)
    {
        var json = await SendAsync(method, path, payload, false, ct);
        PhcEnvelope<T>? result;
        try
        {
            if (json.ValueKind != JsonValueKind.Object
                || !json.TryGetProperty("error", out var error)
                || error.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                throw new JsonException();
            result = json.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            logger.LogError("PHC API returned an invalid response envelope for {Path}", path);
            throw new PhcApiException($"PHC API '{path}' returned an unexpected payload. Confirm operation status before retrying.");
        }

        if (result is null || result.Error)
        {
            logger.LogWarning("PHC API returned a business error for {Path}", path);
            throw new PhcApiException($"PHC API '{path}' returned a business error: {result?.Msg ?? "empty response"}. Confirm operation status before retrying.");
        }
        return result;
    }

    public Task<JsonElement> ExecutarScriptAsync(ExecutarScriptRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Expressao))
        {
            logger.LogWarning("PHC API script rejected: empty expression");
            throw new PhcApiException("expressao must not be empty.");
        }
        if (request.Resumo?.Length > 120)
        {
            logger.LogWarning("PHC API script rejected: summary exceeds 120 characters");
            throw new PhcApiException("resumo must not exceed 120 characters.");
        }

        return SendAsync(HttpMethod.Post, "api/scripts/executar",
            JsonContent.Create(request, PhcJsonContext.Default.ExecutarScriptRequest), false, ct);
    }

    private async Task<JsonElement> SendAsync(
        HttpMethod method, string path, HttpContent? payload, bool allowText, CancellationToken ct)
    {
        using var content = payload;
        try
        {
            var settings = options.Value;
            var baseUri = ValidateConfiguration(settings);
            using var client = httpClientFactory.CreateClient(HttpClientName);
            var login = await GetLoginAsync(client, baseUri, settings, ct);

            using var request = CreateRequest(method, new Uri(baseUri, path), settings);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);
            request.Content = content;

            logger.LogDebug("PHC API {Method} {Path}", method, path);
            using var response = await client.SendAsync(request, ct);
            logger.LogDebug("PHC API {Path} returned HTTP {Status}", path, (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await _loginLock.WaitAsync(ct);
                try
                {
                    if (ReferenceEquals(_login, login))
                        _login = null;
                }
                finally { _loginLock.Release(); }
            }

            // Never replay a write: an error does not prove that no invoice or other change was made.
            EnsureSuccess(response, path);
            if (allowText && response.Content.Headers.ContentType?.MediaType == "text/plain")
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                return JsonSerializer.SerializeToElement(text, PhcJsonContext.Default.String);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.Clone();
        }
        catch (HttpRequestException)
        {
            logger.LogError("PHC API transport failure for {Path}", path);
            throw new PhcApiException("Could not contact PHC API. For write operations, confirm execution status before retrying.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError("PHC API timeout for {Path}", path);
            throw new PhcApiException("PHC API request timed out. For write operations, confirm execution status before retrying.");
        }
        catch (JsonException)
        {
            logger.LogError("PHC API returned invalid JSON for {Path}", path);
            throw new PhcApiException("PHC API returned an unexpected response. For write operations, confirm execution status before retrying.");
        }
        catch (PhcApiException ex)
        {
            logger.LogError("PHC API request failed: {Reason}", ex.Message);
            throw;
        }
    }

    private async Task<PhcApiLoginResponse> GetLoginAsync(
        HttpClient client, Uri baseUri, PhcApiOptions settings, CancellationToken ct)
    {
        await _loginLock.WaitAsync(ct);
        try
        {
            if (_login is not null && _login.ExpiresAtUtc > clock.GetUtcNow().AddSeconds(30))
                return _login;

            using var request = CreateRequest(HttpMethod.Post, new Uri(baseUri, "login"), settings);
            request.Content = JsonContent.Create(
                new PhcApiLoginRequest(settings.Username, settings.Password),
                PhcJsonContext.Default.PhcApiLoginRequest);
            using var response = await client.SendAsync(request, ct);
            EnsureSuccess(response, "login");
            var login = await response.Content.ReadFromJsonAsync(PhcJsonContext.Default.PhcApiLoginResponse, ct);
            if (login is null || string.IsNullOrWhiteSpace(login.Token) || login.ExpiresAtUtc <= clock.GetUtcNow())
                throw new PhcApiException("PHC API login returned a missing token or invalid expiry.");

            _login = login;
            return login;
        }
        finally { _loginLock.Release(); }
    }

    private static Uri ValidateConfiguration(PhcApiOptions settings)
    {
        if (!Uri.TryCreate(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new PhcApiException("Configure PhcApi:BaseUrl as the HTTPS application root, without /api.");
        if (string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password))
            throw new PhcApiException("Configure PhcApi:Username and PhcApi:Password in user-secrets or environment variables.");
        if (settings.TimeoutSeconds <= 0)
            throw new PhcApiException("PhcApi:TimeoutSeconds must be greater than zero.");
        return uri;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, PhcApiOptions settings)
    {
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            request.Headers.Add("X-API-Key", settings.ApiKey);
        return request;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string path)
    {
        if (response.IsSuccessStatusCode)
            return;
        var hint = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? " Check the API credentials, permissions and whether PhcApi:ApiKey is required."
            : " For write operations, confirm execution status before retrying.";
        throw new PhcApiException($"PHC API '{path}' returned HTTP {(int)response.StatusCode}.{hint}");
    }

    private static string AccountingPath(string report, int? ano, int? mes)
    {
        var query = new List<string>();
        if (ano.HasValue)
            query.Add("ano=" + ano.Value.ToString(CultureInfo.InvariantCulture));
        if (mes.HasValue)
            query.Add("mes=" + mes.Value.ToString(CultureInfo.InvariantCulture));
        return "api/contabilidade/" + report + (query.Count == 0 ? "" : "?" + string.Join("&", query));
    }

    private List<string> CommercialQuery(ComercialQuery filters)
    {
        if (filters.Year is < 1 or > 9999)
            RejectInput("year must be between 1 and 9999.");
        var query = new List<string>();
        if (filters.Year.HasValue)
            query.Add("year=" + filters.Year.Value.ToString(CultureInfo.InvariantCulture));
        if (filters.ClientNumber.HasValue)
            query.Add("clientNumber=" + filters.ClientNumber.Value.ToString(CultureInfo.InvariantCulture));
        if (filters.ClientName is not null)
            query.Add("clientName=" + Uri.EscapeDataString(filters.ClientName));
        if (filters.StartDate is not null)
            query.Add("startDate=" + Uri.EscapeDataString(filters.StartDate));
        if (filters.EndDate is not null)
            query.Add("endDate=" + Uri.EscapeDataString(filters.EndDate));
        if (filters.IsClosed.HasValue)
            query.Add("isClosed=" + (filters.IsClosed.Value ? "true" : "false"));
        return query;
    }

    private static string WithQuery(string path, List<string> query) =>
        path + (query.Count == 0 ? "" : "?" + string.Join("&", query));

    private void ValidateInvoice(CriarFaturaPedidoDto request)
    {
        if (request?.Options is null)
            RejectInput("options is required.");
        var options = request.Options;
        if (options.GetPDFPrintOut is < 0 or > 1)
            RejectInput("getPDFPrintOut must be 0 or 1.");
        if (options.Orders is null || options.Orders.Length == 0)
            RejectInput("orders must contain at least one order.");
        foreach (var order in options.Orders)
        {
            if (order is null || string.IsNullOrWhiteSpace(order.OrderID))
                RejectInput("Each order must have a non-empty OrderID.");
            if (order.OrderLineID?.Any(line => line is null) == true)
                RejectInput("OrderLineID must contain strings, not null values.");
        }
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private void RejectInput(string message)
    {
        logger.LogWarning("PHC API request rejected: {Reason}", message);
        throw new PhcApiException(message);
    }

    public void Dispose() => _loginLock.Dispose();
}
