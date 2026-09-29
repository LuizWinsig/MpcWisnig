using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
using winsig.McpServer.Configuration;
using winsig.McpServer.Models;
using winsig.McpServer.Soap;
using winsig.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

// Always load user-secrets (not only in Development) so PHC credentials
// are available regardless of how the server is launched.
builder.Configuration.AddUserSecrets<Program>(optional: true);

// PHC configuration (appsettings.json / user-secrets / environment variables).
builder.Services.Configure<PhcOptions>(builder.Configuration.GetSection(PhcOptions.SectionName));

// Named HttpClient for the PHC SOAP web service, with configurable timeout.
builder.Services.AddHttpClient(PhcSoapClient.HttpClientName, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PhcOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddSingleton<IPhcSoapClient, PhcSoapClient>();

// Microsoft Entra ID (Office365) authentication.
var entra = builder.Configuration.GetSection(EntraOptions.SectionName).Get<EntraOptions>() ?? new EntraOptions();
builder.Services.Configure<EntraOptions>(builder.Configuration.GetSection(EntraOptions.SectionName));

var serverUrl = builder.Configuration["McpServerUrl"] ?? "http://localhost:6194";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    // The MCP scheme issues the 401 challenge with the resource-metadata URL
    // so MCP clients can discover the Entra authorization server.
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MetadataAddress = $"https://login.microsoftonline.com/{entra.TenantId}/v2.0/.well-known/openid-configuration";
    // Keep original JWT claim names ("roles", "preferred_username") instead of legacy SOAP-style mappings.
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        // Entra can issue v1 or v2 tokens depending on the app manifest.
        ValidIssuers =
        [
            $"https://login.microsoftonline.com/{entra.TenantId}/v2.0",
            $"https://sts.windows.net/{entra.TenantId}/"
        ],
        ValidateAudience = true,
        ValidAudiences = [entra.ApiAudience, entra.ApiClientId],
        ValidateLifetime = true,
        RoleClaimType = "roles",
        NameClaimType = "preferred_username"
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new ProtectedResourceMetadata
    {
        Resource = serverUrl,
        AuthorizationServers = { $"https://login.microsoftonline.com/{entra.TenantId}/v2.0" },
        ScopesSupported = [entra.Scope],
        ResourceName = "winsig PHC MCP Server"
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// Combine the MCP SDK defaults with the source-generated PHC JSON context (AOT friendly).
var mcpJsonOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
mcpJsonOptions.TypeInfoResolverChain.Add(PhcJsonContext.Default);

// Add the MCP services: the transport to use (http) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        // Stateless mode is recommended for servers that don't need
        // server-to-client requests like sampling or elicitation.
        // See https://csharp.sdk.modelcontextprotocol.io/concepts/transports/transports.html for details.
        options.Stateless = true;
    })
    .WithTools<RecognizedHoursTools>(mcpJsonOptions)
    .WithTools<UsersTools>(mcpJsonOptions);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// All MCP endpoints require a valid Entra ID token.
app.MapMcp().RequireAuthorization();

app.Run();
