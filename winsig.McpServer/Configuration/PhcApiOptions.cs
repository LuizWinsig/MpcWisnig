namespace winsig.McpServer.Configuration;

public sealed class PhcApiOptions
{
    public const string SectionName = "PhcApi";

    // Application root, e.g. https://suporte.winsig.pt/PhcApi/ (without /api).
    public string BaseUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 100;
}
