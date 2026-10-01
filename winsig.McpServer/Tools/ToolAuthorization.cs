using System.Security.Claims;
using ModelContextProtocol;

namespace winsig.McpServer.Tools;

internal static class ToolAuthorization
{
    public static void RequireRole(ClaimsPrincipal? user, ILogger logger, string role)
    {
        if (user?.Identity?.IsAuthenticated != true)
            throw new McpException("Access denied: request is not authenticated.");

        if (!user.IsInRole(role))
        {
            var name = user.Identity?.Name ?? "unknown";
            logger.LogWarning("User {User} attempted to use a tool requiring role {Role}", name, role);
            throw new McpException($"Access denied: user '{name}' does not have the required role '{role}'.");
        }
    }
}
