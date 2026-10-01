using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace winsig.McpServer.Models;

public sealed record ComercialQuery(
    int? Year = null,
    int? ClientNumber = null,
    string? ClientName = null,
    string? StartDate = null,
    string? EndDate = null,
    bool? IsClosed = null);

public class DocumentoAbertoDto
{
    public string? OrderId { get; set; }
    public int DocumentType { get; set; }
    public string? DocumentName { get; set; }
    public int OrderNumber { get; set; }
    public int Year { get; set; }
    public string? OrderDate { get; set; }
    public int EntityNumber { get; set; }
    public int Establishment { get; set; }
    public string? EntityName { get; set; }
    public bool IsClosed { get; set; }
    public double DocumentTotal { get; set; }
}

public sealed class EncomendaDetalheDto : DocumentoAbertoDto
{
    public List<EncomendaLinhaDto>? Lines { get; set; }
}

public sealed class EncomendaLinhaDto
{
    public string? LineId { get; set; }
    public double LineOrder { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public double OrderedQuantity { get; set; }
    public double FulfilledQuantity { get; set; }
    public double PendingQuantity { get; set; }
    public double LineTotal { get; set; }
    public List<StockArmazemDto>? Stock { get; set; }
}

public sealed class StockArmazemDto
{
    public string? Warehouse { get; set; }
    public double Stock { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CriarFaturaEncomendaDto
{
    [Required, MinLength(1)]
    [JsonPropertyName("OrderID")]
    [Description("Order identifier returned as orderId by the order tools; not the numeric orderNumber.")]
    public required string OrderID { get; init; }

    [JsonPropertyName("OrderLineID")]
    [Description("Optional line identifiers returned as lineId by get_encomendas_detalhe.")]
    public string[]? OrderLineID { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CriarFaturaOpcoesDto
{
    [Range(0, 1)]
    [Description("0: do not request a PDF; 1: request a PDF printout.")]
    public int GetPDFPrintOut { get; init; }

    [Description("Whether to close the orders when invoicing.")]
    public bool CloseOrder { get; init; }

    [Required, MinLength(1)]
    [Description("At least one order, optionally with selected line identifiers.")]
    public required CriarFaturaEncomendaDto[] Orders { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CriarFaturaPedidoDto
{
    [Required]
    public required CriarFaturaOpcoesDto Options { get; init; }
}

public sealed class CriarFaturaResultadoDto
{
    public int Client { get; set; }
    public int Establishment { get; set; }
    public string? ClientName { get; set; }
    public string? Ftstamp { get; set; }
    public int DocumentNumber { get; set; }
    public DateTime DocumentDate { get; set; }
    public string? DocumentName { get; set; }
    public double DocumentTotal { get; set; }
    public string? DocumentAtCud { get; set; }
    public string? DocumentAtDocNumber { get; set; }
    public string? PdfDocumentType { get; set; }
    public string? PdfBinary { get; set; }
    public int LinesInvoiced { get; set; }
    public List<FaturaEncomendaResultadoDto>? Orders { get; set; }
    public string? Url { get; set; }
}

public sealed class FaturaEncomendaResultadoDto
{
    public string? Order { get; set; }
    public int LinesRequested { get; set; }
    public int LinesInvoiced { get; set; }
    public string[]? LinesNotFound { get; set; }
    public bool OrderClosed { get; set; }
}

public sealed class UtilizadorDto
{
    [JsonPropertyName("UserNumber")]
    public double UserNumber { get; set; }

    [JsonPropertyName("UserCode")]
    public string? UserCode { get; set; }

    [JsonPropertyName("UserName")]
    public string? UserName { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }
}
