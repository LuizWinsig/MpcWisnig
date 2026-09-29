using System.Text.Json.Serialization;

namespace winsig.McpServer.Models;

/// <summary>Empty parameter payload for the GET_USERS PHC code.</summary>
public sealed record GetUsersParameter;

/// <summary>User entry returned by GET_USERS.</summary>
public sealed class UserEntry
{
    [JsonPropertyName("UserNumber")]
    public decimal? UserNumber { get; set; }

    [JsonPropertyName("UserCode")]
    public string? UserCode { get; set; }

    [JsonPropertyName("UserName")]
    public string? UserName { get; set; }

    [JsonPropertyName("Email")]
    public string? Email { get; set; }
}
