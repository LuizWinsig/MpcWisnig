namespace winsig.McpServer.Configuration;

/// <summary>
/// Configuration for the PHC SOAP web service (wscript.asmx / RunCode).
/// Values come from appsettings.json, user-secrets or environment variables.
/// </summary>
public sealed class PhcOptions
{
    public const string SectionName = "Phc";

    /// <summary>Base URL of the PHC server, e.g. "https://phc.example.com".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>PHC web service user name (shared by all tools).</summary>
    public string User { get; set; } = string.Empty;

    /// <summary>PHC web service password (shared by all tools). Never logged.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>HTTP timeout, in seconds, for calls to the PHC web service.</summary>
    public int TimeoutSeconds { get; set; } = 100;
}
