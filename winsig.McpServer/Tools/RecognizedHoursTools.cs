using System.ComponentModel;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Server;
using winsig.McpServer.Models;
using winsig.McpServer.Soap;

namespace winsig.McpServer.Tools;

/// <summary>
/// MCP tools for recognized hours operations, backed by the PHC wscript.asmx / RunCode web service.
/// To add a new tool: add a method here with <see cref="McpServerToolAttribute"/> and <see cref="DescriptionAttribute"/>,
/// and reuse <see cref="BasePHCTool.RunAndDeserializeAsync{TEntry}"/>.
/// </summary>
[McpServerToolType]
public sealed class RecognizedHoursTools : BasePHCTool
{
    public RecognizedHoursTools(
        IPhcSoapClient phcClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RecognizedHoursTools> logger)
        : base(phcClient, httpContextAccessor, (ILogger)logger)
    {
    }

    [McpServerTool(Name = "get_recognized_hours", Title = "Gets recognized hours per technician/project from PHC between two dates")]
    [Description("Gets recognized hours per technician/project from PHC between two dates. By default returns only the authenticated user's hours; pass technicianEmail to filter by another technician or \"*\" for all technicians (requires Phc.Admin role).")]
    public async Task<List<RecognizedHoursEntry>> GetRecognizedHoursAsync(
        [Description("Start date of the period (inclusive), e.g. 2025-01-01.")] DateTime startDate,
        [Description("End date of the period (inclusive), e.g. 2025-01-31.")] DateTime endDate,
        [Description("Optional technician email to filter results. Defaults to the authenticated user's email; use \"*\" for all technicians.")] string? technicianEmail = null,
        CancellationToken ct = default)
    {
        RequireRole("Phc.Read");

        var parameter = new GetRecognizedHoursParameter(
            startDate.ToString(DateFormat),
            endDate.ToString(DateFormat),
            ResolveTechnicianEmail(technicianEmail));

        return await RunAndDeserializeAsync(
            "GET_RECOGNIZEDHOURS",
            parameter,
            PhcJsonContext.Default.PhcEnvelopeListRecognizedHoursEntry,
            ct);
    }

    [McpServerTool(Name = "get_detail_recognized_hours", Title = "Gets detailed recognized hours (including absences) from PHC between two dates")]
    [Description("Gets detailed recognized hours (including absences) from PHC between two dates. By default returns only the authenticated user's hours; pass technicianEmail to filter by another technician or \"*\" for all technicians (requires Phc.Admin role). Optionally filtered by project id.")]
    public async Task<List<DetailRecognizedHoursEntry>> GetDetailRecognizedHoursAsync(
        [Description("Start date of the period (inclusive), e.g. 2025-01-01.")] DateTime startDate,
        [Description("End date of the period (inclusive), e.g. 2025-01-31.")] DateTime endDate,
        [Description("Optional technician email to filter results. Defaults to the authenticated user's email; use \"*\" for all technicians.")] string? technicianEmail = null,
        [Description("Optional project id to filter results.")] string? projectId = null,
        CancellationToken ct = default)
    {
        RequireRole("Phc.Read");

        var parameter = new GetDetailRecognizedHoursParameter(
            startDate.ToString(DateFormat),
            endDate.ToString(DateFormat),
            ResolveTechnicianEmail(technicianEmail),
            string.IsNullOrWhiteSpace(projectId) ? "" : projectId);

        var entries = await RunAndDeserializeAsync(
            "GET_DETAILRECOGNIZEDHOURS",
            parameter,
            PhcJsonContext.Default.PhcEnvelopeListDetailRecognizedHoursEntry,
            ct);

        // PHC pads some text fields with trailing spaces.
        foreach (var entry in entries)
        {
            entry.Type = entry.Type?.Trim();
        }

        return entries;
    }
}
