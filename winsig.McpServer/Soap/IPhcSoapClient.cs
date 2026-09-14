namespace winsig.McpServer.Soap;

/// <summary>
/// Shared client for the PHC wscript.asmx SOAP 1.2 web service.
/// All MCP tools go through <see cref="RunCodeAsync"/> with their own PHC code and parameter payload.
/// </summary>
public interface IPhcSoapClient
{
    /// <summary>
    /// Invokes the PHC RunCode operation with the given <paramref name="code"/> and
    /// <paramref name="parameter"/> (serialized to JSON) and returns the raw JSON result string.
    /// The parameter type must be registered in <see cref="Models.PhcJsonContext"/>.
    /// </summary>
    Task<string> RunCodeAsync(string code, object parameter, CancellationToken ct = default);
}
