using System.ComponentModel;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Server;
using winsig.McpServer.Models;
using winsig.McpServer.Soap;

namespace winsig.McpServer.Tools;

/// <summary>
/// MCP tools for user management operations, backed by the PHC wscript.asmx / RunCode web service.
/// </summary>
[McpServerToolType]
public sealed class UsersTools : BasePHCTool
{
    public UsersTools(
        IPhcSoapClient phcClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UsersTools> logger)
        : base(phcClient, httpContextAccessor, (ILogger)logger)
    {
    }

    [McpServerTool(Name = "get_users", Title = "Gets the list of all users from PHC")]
    [Description("Gets the list of all users from PHC (requires Phc.Read role).")]
    public async Task<List<UserEntry>> GetUsersAsync(CancellationToken ct = default)
    {
        RequireRole("Phc.Read");

        var parameter = new GetUsersParameter();

        return await RunAndDeserializeAsync(
            "GET_USERS",
            parameter,
            PhcJsonContext.Default.PhcEnvelopeListUserEntry,
            ct);
    }
}
