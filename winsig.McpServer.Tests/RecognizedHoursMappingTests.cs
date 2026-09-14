using System.Text.Json;
using winsig.McpServer.Models;

namespace winsig.McpServer.Tests;

public class RecognizedHoursMappingTests
{
    [Fact]
    public void Deserialize_SummaryEnvelope_MapsAllFields()
    {
        const string json = """
            {
              "error": false,
              "msg": "Recognized hours retrieved successfully",
              "data": [
                {
                  "TechnicianID": 102.0,
                  "TechnicianName": "Alexandre Freitas",
                  "ProjectID": "23AC698",
                  "Project": "23 Bolsa de Horas PHC",
                  "InterventionHours": 1.50,
                  "BilledHours": 1.50,
                  "RecognizedHours": 0.00,
                  "Area": "Lisboa",
                  "Email": "alexandre.freitas@winsig.pt"
                }
              ]
            }
            """;

        var envelope = JsonSerializer.Deserialize(json, PhcJsonContext.Default.PhcEnvelopeListRecognizedHoursEntry);

        Assert.NotNull(envelope);
        Assert.False(envelope.Error);
        Assert.Equal("Recognized hours retrieved successfully", envelope.Msg);
        var entry = Assert.Single(envelope.Data!);
        Assert.Equal(102.0m, entry.TechnicianID);
        Assert.Equal("Alexandre Freitas", entry.TechnicianName);
        Assert.Equal("23AC698", entry.ProjectID);
        Assert.Equal("23 Bolsa de Horas PHC", entry.Project);
        Assert.Equal(1.50m, entry.InterventionHours);
        Assert.Equal(1.50m, entry.BilledHours);
        Assert.Equal(0.00m, entry.RecognizedHours);
        Assert.Equal("Lisboa", entry.Area);
        Assert.Equal("alexandre.freitas@winsig.pt", entry.Email);
    }

    [Fact]
    public void Deserialize_DetailEnvelope_MapsAllFields()
    {
        const string json = """
            {
              "error": false,
              "msg": "Recognized detail hours retrieved successfully",
              "data": [
                {
                  "TechnicianID": 102.0,
                  "Technician": "Alexandre Freitas",
                  "ProjectID": "24JA288",
                  "Project": "24CM C. Editorial Enc22007854",
                  "InterventionHours": 2.50,
                  "MarkedHoursToInvoice": 2.50,
                  "Area": "Lisboa",
                  "Date": "2025-09-01T00:00:00",
                  "Type": "Assistência         ",
                  "Email": "alexandre.freitas@winsig.pt"
                }
              ]
            }
            """;

        var envelope = JsonSerializer.Deserialize(json, PhcJsonContext.Default.PhcEnvelopeListDetailRecognizedHoursEntry);

        Assert.NotNull(envelope);
        Assert.False(envelope.Error);
        var entry = Assert.Single(envelope.Data!);
        Assert.Equal(102.0m, entry.TechnicianID);
        Assert.Equal("Alexandre Freitas", entry.Technician);
        Assert.Equal("24JA288", entry.ProjectID);
        Assert.Equal(2.50m, entry.InterventionHours);
        Assert.Equal(2.50m, entry.MarkedHoursToInvoice);
        Assert.Equal("2025-09-01T00:00:00", entry.Date);
        Assert.Equal("Assistência         ", entry.Type);
    }

    [Fact]
    public void Deserialize_BusinessError_IsExposed()
    {
        const string json = """{ "error": true, "msg": "Invalid date range", "data": null }""";

        var envelope = JsonSerializer.Deserialize(json, PhcJsonContext.Default.PhcEnvelopeListRecognizedHoursEntry);

        Assert.NotNull(envelope);
        Assert.True(envelope.Error);
        Assert.Equal("Invalid date range", envelope.Msg);
        Assert.Null(envelope.Data);
    }

    [Fact]
    public void Deserialize_MissingAndNullFields_AreNullable()
    {
        const string json = """{ "error": false, "data": [{ "TechnicianID": 5, "Project": null }] }""";

        var envelope = JsonSerializer.Deserialize(json, PhcJsonContext.Default.PhcEnvelopeListRecognizedHoursEntry);

        var entry = Assert.Single(envelope!.Data!);
        Assert.Equal(5m, entry.TechnicianID);
        Assert.Null(entry.Project);
        Assert.Null(entry.InterventionHours);
        Assert.Null(entry.Email);
    }

    [Fact]
    public void Deserialize_NumbersAsStrings_AreAccepted()
    {
        const string json = """{ "error": false, "data": [{ "InterventionHours": "8", "BilledHours": "4.25" }] }""";

        var envelope = JsonSerializer.Deserialize(json, PhcJsonContext.Default.PhcEnvelopeListRecognizedHoursEntry);

        var entry = Assert.Single(envelope!.Data!);
        Assert.Equal(8m, entry.InterventionHours);
        Assert.Equal(4.25m, entry.BilledHours);
    }

    [Fact]
    public void Serialize_ParameterPayload_UsesPhcFieldNamesAndOmitsNulls()
    {
        var parameter = new GetDetailRecognizedHoursParameter("2025-01-01", "2025-01-31", null, "P100");

        string json = JsonSerializer.Serialize(parameter, typeof(GetDetailRecognizedHoursParameter), PhcJsonContext.Default);

        Assert.Contains("\"StartDate\":\"2025-01-01\"", json);
        Assert.Contains("\"EndDate\":\"2025-01-31\"", json);
        Assert.Contains("\"ProjectId\":\"P100\"", json);
        Assert.DoesNotContain("TechnicianEmail", json);
    }
}
