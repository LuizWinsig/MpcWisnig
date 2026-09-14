# winsig.McpServer — PHC MCP Server

MCP server (Streamable HTTP transport, ASP.NET Core) that exposes PHC operations as MCP tools.
All tools call the same PHC SOAP 1.2 web service (`wscript.asmx`, operation `RunCode`) through a shared `PhcSoapClient`.

## Configuration

Settings live in the `Phc` section (`appsettings.json`, user-secrets or environment variables — never hardcode credentials):

| Key | Description |
| --- | --- |
| `Phc:BaseUrl` | Base URL of the PHC server (the client posts to `{BaseUrl}/ws/wscript.asmx`) |
| `Phc:User` | PHC web service user (shared by all tools) |
| `Phc:Password` | PHC web service password (never logged) |
| `Phc:TimeoutSeconds` | HTTP timeout for PHC calls (default 100) |

Recommended for local development (user-secrets):

```bash
cd winsig.McpServer
dotnet user-secrets set "Phc:BaseUrl" "https://phc.example.com"
dotnet user-secrets set "Phc:User" "myuser"
dotnet user-secrets set "Phc:Password" "mypassword"
```

Or via environment variables: `Phc__BaseUrl`, `Phc__User`, `Phc__Password`.

The listening URL is configurable via `Urls` in `appsettings.json` (default `http://localhost:6194`) or the `ASPNETCORE_URLS` environment variable.

## Running locally

```bash
dotnet run --project winsig.McpServer
```

MCP client configuration (VS Code / Visual Studio):

```json
{
  "servers": {
    "winsig.McpServer": {
      "type": "http",
      "url": "http://localhost:6194"
    }
  }
}
```

## Available tools

| Tool | PHC code | Parameters |
| --- | --- | --- |
| `get_recognized_hours` | `GET_RECOGNIZEDHOURS` | `startDate`, `endDate`, `technicianEmail?` |
| `get_detail_recognized_hours` | `GET_DETAILRECOGNIZEDHOURS` | `startDate`, `endDate`, `technicianEmail?`, `projectId?` |

Both return a list of recognized-hours entries (`TechnicianID`, `Technician`, `ProjectID`, `Project`, `InterventionHours`, `MarkedHoursToInvoice`, `Area`, `Date`, `Type`, `Email`).

## Adding a new tool

1. Add the parameter record / response DTOs in `Models/` and register them in `PhcJsonContext`.
2. Add a method to a `[McpServerToolType]` class in `Tools/` (or create a new class and register it in `Program.cs` with `.WithTools<...>(mcpJsonOptions)`).
3. Call `IPhcSoapClient.RunCodeAsync("YOUR_PHC_CODE", parameter, ct)` and deserialize the JSON result.

No changes to `PhcSoapClient` or configuration are needed.

## Project layout

- `Configuration/` — `PhcOptions` (bound to the `Phc` section)
- `Soap/` — `IPhcSoapClient`, `PhcSoapClient` (SOAP 1.2 envelope + HTTP), `PhcSoapResponseParser`, `PhcSoapException`
- `Models/` — parameter payloads, response DTOs and the source-generated `PhcJsonContext`
- `Tools/` — MCP tool classes (`RecognizedHoursTools`)
- `../winsig.McpServer.Tests/` — unit tests for SOAP response parsing and DTO mapping

## Tests

```bash
dotnet test winsig.McpServer.Tests
```

## More information

- [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
- [Official MCP Documentation](https://modelcontextprotocol.io/)
