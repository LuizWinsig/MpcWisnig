using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using winsig.McpServer.Api;
using winsig.McpServer.Models;

namespace winsig.McpServer.Tools;

[McpServerToolType]
public sealed class PhcApiTools(
    IPhcApiClient client,
    IHttpContextAccessor httpContextAccessor,
    ILogger<PhcApiTools> logger)
{
    [McpServerTool(Name = "get_balancete", ReadOnly = true, Idempotent = true, Title ="Gets the PHC accounting trial balance", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Gets the PHC accounting trial balance. Requires Phc.Read. Omitted year/month are handled by the API.")]
    public Task<JsonElement> GetBalanceteAsync(
        [Description("Optional accounting year.")] int? ano = null,
        [Description("Optional accounting month.")] int? mes = null,
        CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.GetBalanceteAsync(ano, mes, ct));

    [McpServerTool(Name = "get_fluxo_caixa", ReadOnly = true, Idempotent = true, Title ="Gets the PHC accounting cash flow", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Gets the PHC accounting cash flow. Requires Phc.Read. Omitted year/month are handled by the API.")]
    public Task<JsonElement> GetFluxoCaixaAsync(
        [Description("Optional accounting year.")] int? ano = null,
        [Description("Optional accounting month.")] int? mes = null,
        CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.GetFluxoCaixaAsync(ano, mes, ct));

    [McpServerTool(Name = "get_phc_api_health", ReadOnly = true, Idempotent = true, Title ="Gets the PHC REST API health status", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Gets the PHC REST API health status. Requires Phc.Read.")]
    public Task<JsonElement> GetHealthAsync(CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.GetHealthAsync(ct));

    [McpServerTool(Name = "get_encomendas", ReadOnly = true, Idempotent = true, Title = "Gets PHC orders", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Gets PHC orders with optional filters. Requires Phc.Read. Returns orderId for use in invoice creation; no filters are imposed when omitted.")]
    public Task<PhcEnvelope<List<DocumentoAbertoDto>>> GetEncomendasAsync(
        [Description("Optional numeric order number.")] int? orderNumber = null,
        [Description("Optional year, from 1 to 9999."), Range(1, 9999)] int? year = null,
        [Description("Optional client number.")] int? clientNumber = null,
        [Description("Optional client name.")] string? clientName = null,
        [Description("Optional start date string accepted by PHC.")] string? startDate = null,
        [Description("Optional end date string accepted by PHC.")] string? endDate = null,
        [Description("Optional closed status: true for closed, false for open; omit for either.")] bool? isClosed = null,
        CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.GetEncomendasAsync(orderNumber,
            new(year, clientNumber, clientName, startDate, endDate, isClosed), ct));

    [McpServerTool(Name = "get_encomendas_detalhe", ReadOnly = true, Idempotent = true, Title = "Gets PHC order details", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Gets PHC orders with their lines, quantities and totals. Requires Phc.Read. Returns orderId and lineId for invoice creation.")]
    public Task<PhcEnvelope<List<EncomendaDetalheDto>>> GetEncomendasDetalheAsync(
        [Description("Optional array of numeric order numbers.")] int[]? orderNumbers = null,
        [Description("Optional year, from 1 to 9999."), Range(1, 9999)] int? year = null,
        [Description("Optional client number.")] int? clientNumber = null,
        [Description("Optional client name.")] string? clientName = null,
        [Description("Optional start date string accepted by PHC.")] string? startDate = null,
        [Description("Optional end date string accepted by PHC.")] string? endDate = null,
        [Description("Optional closed status: true for closed, false for open; omit for either.")] bool? isClosed = null,
        CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.GetEncomendasDetalheAsync(orderNumbers,
            new(year, clientNumber, clientName, startDate, endDate, isClosed), ct));

    [McpServerTool(Name = "criar_fatura_de_encomendas", ReadOnly = false, Destructive = true, Idempotent = false,
        Title = "Creates a PHC invoice from orders", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Creates a real invoice from orders and may close them. Requires Phc.Admin. Obtain explicit user confirmation for the selected orders/lines and options before calling. Use orderId/lineId from the order tools, not numeric order numbers. Never automatically retry after an error or timeout; first check in PHC whether the invoice was created.")]
    public Task<PhcEnvelope<CriarFaturaResultadoDto>> CriarFaturaAsync(
        [Description("Invoice options: orders with OrderID/OrderLineID, getPDFPrintOut (0 or 1), and closeOrder.")] CriarFaturaOpcoesDto options,
        CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.CriarFaturaAsync(new() { Options = options }, ct));

    [McpServerTool(Name = "get_utilizadores", ReadOnly = true, Idempotent = true, Title = "Gets PHC REST API users", IconSource = "https://suporte.winsig.pt/mcp/assets/logo.png")]
    [Description("Gets PHC users from the REST API /api/utilizadores. Requires Phc.Read. The existing get_users tool continues to use SOAP.")]
    public Task<PhcEnvelope<List<UtilizadorDto>>> GetUtilizadoresAsync(CancellationToken ct = default) =>
        RunAsync("Phc.Read", () => client.GetUtilizadoresAsync(ct));

    private async Task<T> RunAsync<T>(string role, Func<Task<T>> action)
    {
        ToolAuthorization.RequireRole(httpContextAccessor.HttpContext?.User, logger, role);
        try
        {
            return await action();
        }
        catch (PhcApiException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}
