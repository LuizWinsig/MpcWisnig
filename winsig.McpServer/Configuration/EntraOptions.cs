namespace winsig.McpServer.Configuration;

/// <summary>
/// Microsoft Entra ID (Office365) authentication settings for the MCP server.
/// </summary>
public sealed class EntraOptions
{
    public const string SectionName = "Entra";

    /// <summary>Directory (tenant) ID.</summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>Application (client) ID of the server/API app registration (winsig-mcp-server).</summary>
    public string ApiClientId { get; set; } = string.Empty;

    /// <summary>Application (client) ID of the public client app registration (winsig-mcp-client).</summary>
    public string ClientAppId { get; set; } = string.Empty;

    /// <summary>Application ID URI of the API (defaults to api://{ApiClientId}).</summary>
    public string ApiAudience => $"api://{ApiClientId}";

    /// <summary>OAuth scope requested by clients.</summary>
    public string Scope => $"{ApiAudience}/access_as_user";

    /// <summary>Entra v2.0 authority for this tenant.</summary>
    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
}
