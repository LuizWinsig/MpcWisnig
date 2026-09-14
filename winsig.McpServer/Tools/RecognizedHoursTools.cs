using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using winsig.McpServer.Models;
using winsig.McpServer.Soap;

namespace winsig.McpServer.Tools;

/// <summary>
/// MCP tools backed by the PHC wscript.asmx / RunCode web service.
/// To add a new tool: add a method here (or a new [McpServerToolType] class) with its own
/// PHC code and parameter payload, and reuse <see cref="IPhcSoapClient.RunCodeAsync"/>.
/// </summary>
[McpServerToolType]
public sealed class RecognizedHoursTools
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly IPhcSoapClient _phcClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RecognizedHoursTools> _logger;

    public RecognizedHoursTools(
        IPhcSoapClient phcClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RecognizedHoursTools> logger)
    {
        _phcClient = phcClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>Ensures the authenticated Office365 user has the given Entra app role.</summary>
    private void RequireRole(string role)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new McpException("Access denied: request is not authenticated.");
        }

        if (!user.IsInRole(role))
        {
            var name = user.Identity?.Name ?? "unknown";
            _logger.LogWarning("User {User} attempted to use a tool requiring role {Role}", name, role);
            throw new McpException($"Access denied: user '{name}' does not have the required role '{role}'.");
        }
    }

    [McpServerTool(Name = "get_recognized_hours")]
    [Description("Gets recognized hours per technician/project from PHC between two dates, optionally filtered by technician email.")]
    public async Task<List<RecognizedHoursEntry>> GetRecognizedHoursAsync(
        [Description("Start date of the period (inclusive), e.g. 2025-01-01.")] DateTime startDate,
        [Description("End date of the period (inclusive), e.g. 2025-01-31.")] DateTime endDate,
        [Description("Optional technician email to filter results.")] string? technicianEmail = null,
        CancellationToken ct = default)
    {
        RequireRole("Phc.Read");

        var parameter = new GetRecognizedHoursParameter(
            startDate.ToString(DateFormat),
            endDate.ToString(DateFormat),
            string.IsNullOrWhiteSpace(technicianEmail) ? "" : technicianEmail);

        return await RunAndDeserializeAsync(
            "GET_RECOGNIZEDHOURS",
            parameter,
            PhcJsonContext.Default.PhcEnvelopeListRecognizedHoursEntry,
            ct);
    }

    [McpServerTool(Name = "get_detail_recognized_hours")]
    [Description("Gets detailed recognized hours (including absences) from PHC between two dates, optionally filtered by technician email and/or project id.")]
    public async Task<List<DetailRecognizedHoursEntry>> GetDetailRecognizedHoursAsync(
        [Description("Start date of the period (inclusive), e.g. 2025-01-01.")] DateTime startDate,
        [Description("End date of the period (inclusive), e.g. 2025-01-31.")] DateTime endDate,
        [Description("Optional technician email to filter results.")] string? technicianEmail = null,
        [Description("Optional project id to filter results.")] string? projectId = null,
        CancellationToken ct = default)
    {
        RequireRole("Phc.ReadDetail");

        var parameter = new GetDetailRecognizedHoursParameter(
            startDate.ToString(DateFormat),
            endDate.ToString(DateFormat),
            string.IsNullOrWhiteSpace(technicianEmail) ? "" : technicianEmail,
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

    private async Task<List<TEntry>> RunAndDeserializeAsync<TEntry>(
        string code,
        object parameter,
        JsonTypeInfo<PhcEnvelope<List<TEntry>>> envelopeTypeInfo,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await _phcClient.RunCodeAsync(code, parameter, ct);
        }
        catch (PhcSoapException ex)
        {
            // Surface a useful message to the MCP client without crashing the server.
            throw new McpException($"PHC call '{code}' failed: {ex.Message}");
        }

        PhcEnvelope<List<TEntry>>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize(json, envelopeTypeInfo);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize PHC response for code {Code}", code);
            throw new McpException($"PHC call '{code}' returned an unexpected payload: {ex.Message}");
        }

        if (envelope is null || envelope.Error)
        {
            throw new McpException($"PHC call '{code}' returned a business error: {envelope?.Msg ?? "empty response"}");
        }

        return envelope.Data ?? [];
    }
}