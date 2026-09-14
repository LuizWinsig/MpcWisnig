using winsig.McpServer.Soap;

namespace winsig.McpServer.Tests;

public class PhcSoapResponseParserTests
{
    private const string ValidResponse = """
        <?xml version="1.0" encoding="utf-8"?>
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
          <soap:Body>
            <RunCodeResponse xmlns="http://www.phc.pt/">
              <RunCodeResult>[{"TechnicianID":"T01","Technician":"Jane Doe"}]</RunCodeResult>
            </RunCodeResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private const string FaultResponse = """
        <?xml version="1.0" encoding="utf-8"?>
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
          <soap:Body>
            <soap:Fault>
              <soap:Code><soap:Value>soap:Receiver</soap:Value></soap:Code>
              <soap:Reason><soap:Text xml:lang="en">Invalid credentials</soap:Text></soap:Reason>
            </soap:Fault>
          </soap:Body>
        </soap:Envelope>
        """;

    [Fact]
    public void ExtractRunCodeResult_ReturnsJsonPayload()
    {
        var result = PhcSoapResponseParser.ExtractRunCodeResult(ValidResponse);

        Assert.Equal("""[{"TechnicianID":"T01","Technician":"Jane Doe"}]""", result);
    }

    [Fact]
    public void ExtractRunCodeResult_SoapFault_ThrowsWithReason()
    {
        var ex = Assert.Throws<PhcSoapException>(() => PhcSoapResponseParser.ExtractRunCodeResult(FaultResponse));

        Assert.Contains("SOAP fault", ex.Message);
        Assert.Contains("Invalid credentials", ex.Message);
    }

    [Fact]
    public void ExtractRunCodeResult_MissingResult_Throws()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope">
              <soap:Body />
            </soap:Envelope>
            """;

        var ex = Assert.Throws<PhcSoapException>(() => PhcSoapResponseParser.ExtractRunCodeResult(xml));

        Assert.Contains("RunCodeResult", ex.Message);
    }

    [Fact]
    public void ExtractRunCodeResult_InvalidXml_Throws()
    {
        var ex = Assert.Throws<PhcSoapException>(() => PhcSoapResponseParser.ExtractRunCodeResult("not xml"));

        Assert.Contains("not valid XML", ex.Message);
    }
}
