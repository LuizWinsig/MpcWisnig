using System.Xml.Linq;

namespace winsig.McpServer.Soap;

/// <summary>
/// Parses the SOAP 1.2 response envelope returned by the PHC wscript.asmx / RunCode operation.
/// </summary>
public static class PhcSoapResponseParser
{
    private static readonly XNamespace Soap12Ns = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace PhcNs = "http://www.phc.pt/";

    /// <summary>
    /// Extracts the JSON string carried inside &lt;RunCodeResult&gt; from a SOAP response envelope.
    /// Throws <see cref="PhcSoapException"/> on SOAP faults or unexpected payloads.
    /// </summary>
    public static string ExtractRunCodeResult(string soapXml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(soapXml);
        }
        catch (Exception ex)
        {
            throw new PhcSoapException("PHC response is not valid XML.", ex);
        }

        var fault = document.Descendants(Soap12Ns + "Fault").FirstOrDefault();
        if (fault is not null)
        {
            var reason = fault.Descendants(Soap12Ns + "Text").FirstOrDefault()?.Value?.Trim()
                         ?? fault.Value.Trim();
            throw new PhcSoapException($"PHC returned a SOAP fault: {reason}");
        }

        var result = document.Descendants(PhcNs + "RunCodeResult").FirstOrDefault();
        if (result is null)
        {
            throw new PhcSoapException("PHC response does not contain a RunCodeResult element.");
        }

        return result.Value;
    }
}
