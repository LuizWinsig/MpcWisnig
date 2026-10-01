using System.Text.Json;
using winsig.McpServer.Models;

namespace winsig.McpServer.Api;

public interface IPhcApiClient
{
    Task<JsonElement> GetBalanceteAsync(int? ano, int? mes, CancellationToken ct = default);
    Task<JsonElement> GetFluxoCaixaAsync(int? ano, int? mes, CancellationToken ct = default);
    Task<JsonElement> GetHealthAsync(CancellationToken ct = default);
    Task<PhcEnvelope<List<DocumentoAbertoDto>>> GetEncomendasAsync(int? orderNumber, ComercialQuery filters, CancellationToken ct = default);
    Task<PhcEnvelope<List<EncomendaDetalheDto>>> GetEncomendasDetalheAsync(int[]? orderNumbers, ComercialQuery filters, CancellationToken ct = default);
    Task<PhcEnvelope<CriarFaturaResultadoDto>> CriarFaturaAsync(CriarFaturaPedidoDto request, CancellationToken ct = default);
    Task<PhcEnvelope<List<UtilizadorDto>>> GetUtilizadoresAsync(CancellationToken ct = default);
    Task<JsonElement> ExecutarScriptAsync(ExecutarScriptRequest request, CancellationToken ct = default);
}
