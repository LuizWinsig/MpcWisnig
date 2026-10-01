using System.Text.Json;
using System.Text.Json.Serialization;

namespace winsig.McpServer.Models;

public sealed record PhcApiLoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

public sealed class PhcApiLoginResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("expiresAtUtc")]
    public DateTimeOffset ExpiresAtUtc { get; set; }
}

public sealed record ExecutarScriptRequest(
    [property: JsonPropertyName("expressao")] string Expressao,
    [property: JsonPropertyName("resumo")] string? Resumo,
    [property: JsonPropertyName("parametros")] JsonElement? Parametros);
