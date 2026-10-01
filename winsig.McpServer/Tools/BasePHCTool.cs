using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using winsig.McpServer.Models;
using winsig.McpServer.Soap;

namespace winsig.McpServer.Tools;

/// <summary>
/// Base class for MCP tool collections backed by PHC SOAP web service.
/// Provides common functionality: authentication, role checks, SOAP client access, logging.
/// Derived classes implement specific tools using <see cref="IPhcSoapClient.RunCodeAsync"/>.
/// </summary>
public abstract class BasePHCTool
{
    protected const string DateFormat = "yyyy-MM-dd";

    protected readonly IPhcSoapClient PhcClient;
    protected readonly IHttpContextAccessor HttpContextAccessor;
    protected readonly ILogger Logger;

    protected BasePHCTool(
        IPhcSoapClient phcClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger)
    {
        PhcClient = phcClient;
        HttpContextAccessor = httpContextAccessor;
        Logger = logger;
    }

    /// <summary>Ensures the authenticated Office365 user has the given Entra app role.</summary>
    protected void RequireRole(string role)
    {
        ToolAuthorization.RequireRole(HttpContextAccessor.HttpContext?.User, Logger, role);
    }

    /// <summary>Gets the email of the authenticated Office365 user from the JWT claims.</summary>
    protected string GetAuthenticatedEmail()
    {
        var user = HttpContextAccessor.HttpContext?.User;
        var email = user?.FindFirst("preferred_username")?.Value
                    ?? user?.FindFirst("email")?.Value
                    ?? user?.FindFirst("upn")?.Value;

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new McpException("Access denied: could not determine the authenticated user's email.");
        }

        return email;
    }

    /// <summary>
    /// Resolves the technician email filter: defaults to the authenticated user's email;
    /// "*" or empty string means all technicians (requires Phc.Admin role).
    /// </summary>
    protected string ResolveTechnicianEmail(string? technicianEmail)
    {
        var user = HttpContextAccessor.HttpContext?.User;

        if (string.IsNullOrWhiteSpace(technicianEmail))
        {
            // Empty/null: use authenticated user's email, unless they explicitly want all (need Phc.Admin)
            return GetAuthenticatedEmail();
        }

        var trimmed = technicianEmail.Trim();
        
        // "*" or empty string in parameter: request all technicians (requires Phc.Admin)
        if (trimmed == "*" || trimmed == "")
        {
            if (user?.Identity?.IsAuthenticated != true || !user.IsInRole("Phc.Admin"))
            {
                var name = user?.Identity?.Name ?? "unknown";
                Logger.LogWarning("User {User} attempted to view all technicians without Phc.Admin role", name);
                throw new McpException($"Access denied: viewing all technicians requires the 'Phc.Admin' role.");
            }

            return ""; // Return empty string to PHC (all technicians)
        }

        // Regular email filter
        return trimmed;
    }

    /// <summary>
    /// Calls the PHC SOAP service and deserializes the response envelope.
    /// Throws McpException on business or deserialization errors.
    /// </summary>
    protected async Task<List<TEntry>> RunAndDeserializeAsync<TEntry>(
        string code,
        object parameter,
        JsonTypeInfo<PhcEnvelope<List<TEntry>>> envelopeTypeInfo,
        CancellationToken ct)
    {
        string json;
        try
        {
            json = await PhcClient.RunCodeAsync(code, parameter, ct);
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
            Logger.LogError(ex, "Failed to deserialize PHC response for code {Code}", code);
            throw new McpException($"PHC call '{code}' returned an unexpected payload: {ex.Message}");
        }

        if (envelope is null || envelope.Error)
        {
            throw new McpException($"PHC call '{code}' returned a business error: {envelope?.Msg ?? "empty response"}");
        }

        return envelope.Data ?? [];
    }
}
