using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using winsig.McpServer.Configuration;
using winsig.McpServer.Models;

namespace winsig.McpServer.Soap;

/// <summary>
/// Default <see cref="IPhcSoapClient"/> implementation.
/// Builds the SOAP 1.2 envelope for wscript.asmx / RunCode, posts it and extracts the JSON result.
/// </summary>
public sealed class PhcSoapClient : IPhcSoapClient
{
    public const string HttpClientName = "phc";

    private static readonly XNamespace Soap12Ns = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace PhcNs = "http://www.phc.pt/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PhcOptions _options;
    private readonly ILogger<PhcSoapClient> _logger;

    public PhcSoapClient(
        IHttpClientFactory httpClientFactory,
        IOptions<PhcOptions> options,
        ILogger<PhcSoapClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> RunCodeAsync(string code, object parameter, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(parameter);

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new PhcSoapException(
                "PHC BaseUrl is not configured. Set 'Phc:BaseUrl' (and 'Phc:User'/'Phc:Password') via appsettings, user-secrets or environment variables.");
        }

        string parameterJson = JsonSerializer.Serialize(parameter, parameter.GetType(), PhcJsonContext.Default);
        string envelope = BuildEnvelope(code, parameterJson);
        string url = $"{_options.BaseUrl.TrimEnd('/')}/ws/wscript.asmx";

        // Never log credentials; only code and parameter payload at Debug level.
        _logger.LogDebug("PHC RunCode request: code={Code}, url={Url}, parameter={Parameter}", code, url, parameterJson);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(url, content, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "PHC RunCode call failed for code {Code}", code);
            throw new PhcSoapException($"Failed to reach PHC web service at '{url}' for code '{code}': {ex.Message}", ex);
        }

        using (response)
        {
            string body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // The body may still carry a SOAP fault with a useful reason.
                TryThrowSoapFault(body, code);
                _logger.LogError("PHC RunCode returned HTTP {StatusCode} for code {Code}", (int)response.StatusCode, code);
                throw new PhcSoapException($"PHC web service returned HTTP {(int)response.StatusCode} for code '{code}'.");
            }

            string result = PhcSoapResponseParser.ExtractRunCodeResult(body);
            _logger.LogDebug("PHC RunCode response for code {Code}: {Result}", code, result);
            return result;
        }
    }

    private static void TryThrowSoapFault(string body, string code)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        try
        {
            PhcSoapResponseParser.ExtractRunCodeResult(body);
        }
        catch (PhcSoapException ex) when (ex.Message.Contains("SOAP fault", StringComparison.OrdinalIgnoreCase))
        {
            throw new PhcSoapException($"{ex.Message} (code '{code}')");
        }
        catch (PhcSoapException)
        {
            // Not a fault; fall through so the caller reports the HTTP status instead.
        }
    }

    private string BuildEnvelope(string code, string parameterJson)
    {
        XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(Soap12Ns + "Envelope",
                new XAttribute(XNamespace.Xmlns + "xsi", xsi),
                new XAttribute(XNamespace.Xmlns + "xsd", xsd),
                new XAttribute(XNamespace.Xmlns + "soap12", Soap12Ns),
                new XElement(Soap12Ns + "Body",
                    new XElement(PhcNs + "RunCode",
                        new XElement(PhcNs + "userName", _options.User),
                        new XElement(PhcNs + "password", _options.Password),
                        new XElement(PhcNs + "code", code),
                        new XElement(PhcNs + "parameter", new XCData(parameterJson))))));

        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + document.ToString(SaveOptions.DisableFormatting);
    }
}
