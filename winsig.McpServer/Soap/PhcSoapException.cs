namespace winsig.McpServer.Soap;

/// <summary>
/// Raised when the PHC web service returns an HTTP error, a SOAP fault
/// or an unexpected/empty payload.
/// </summary>
public sealed class PhcSoapException : Exception
{
    public PhcSoapException(string message) : base(message)
    {
    }

    public PhcSoapException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
