using System.Text.Json.Serialization;

namespace winsig.McpServer.Models;

/// <summary>Parameter payload for the GET_RECOGNIZEDHOURS PHC code.</summary>
public sealed record GetRecognizedHoursParameter(
    [property: JsonPropertyName("StartDate")] string StartDate,
    [property: JsonPropertyName("EndDate")] string EndDate,
    [property: JsonPropertyName("TechnicianEmail")] string? TechnicianEmail);

/// <summary>Parameter payload for the GET_DETAILRECOGNIZEDHOURS PHC code.</summary>
public sealed record GetDetailRecognizedHoursParameter(
    [property: JsonPropertyName("StartDate")] string StartDate,
    [property: JsonPropertyName("EndDate")] string EndDate,
    [property: JsonPropertyName("TechnicianEmail")] string? TechnicianEmail,
    [property: JsonPropertyName("ProjectId")] string? ProjectId);

/// <summary>
/// Standard PHC RunCode response envelope: { "error": bool, "msg": string, "data": [...] }.
/// </summary>
public sealed class PhcEnvelope<TData>
{
    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }

    [JsonPropertyName("data")]
    public TData? Data { get; set; }
}

/// <summary>Summary entry returned by GET_RECOGNIZEDHOURS.</summary>
public sealed class RecognizedHoursEntry
{
    [JsonPropertyName("TechnicianID")]
    public decimal? TechnicianID { get; set; }

    [JsonPropertyName("TechnicianName")]
    public string? TechnicianName { get; set; }

    [JsonPropertyName("ProjectID")]
    public string? ProjectID { get; set; }

    [JsonPropertyName("Project")]
    public string? Project { get; set; }

    [JsonPropertyName("InterventionHours")]
    public decimal? InterventionHours { get; set; }

    [JsonPropertyName("BilledHours")]
    public decimal? BilledHours { get; set; }

    [JsonPropertyName("RecognizedHours")]
    public decimal? RecognizedHours { get; set; }

    [JsonPropertyName("Area")]
    public string? Area { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }
}

/// <summary>Detail entry returned by GET_DETAILRECOGNIZEDHOURS (recognized hours and absences).</summary>
public sealed class DetailRecognizedHoursEntry
{
    [JsonPropertyName("TechnicianID")]
    public decimal? TechnicianID { get; set; }

    [JsonPropertyName("Technician")]
    public string? Technician { get; set; }

    [JsonPropertyName("ProjectID")]
    public string? ProjectID { get; set; }

    [JsonPropertyName("Project")]
    public string? Project { get; set; }

    [JsonPropertyName("InterventionHours")]
    public decimal? InterventionHours { get; set; }

    [JsonPropertyName("MarkedHoursToInvoice")]
    public decimal? MarkedHoursToInvoice { get; set; }

    [JsonPropertyName("Area")]
    public string? Area { get; set; }

    [JsonPropertyName("Date")]
    public string? Date { get; set; }

    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }
}
