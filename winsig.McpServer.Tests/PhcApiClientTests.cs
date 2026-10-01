using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using winsig.McpServer.Api;
using winsig.McpServer.Configuration;
using winsig.McpServer.Models;

namespace winsig.McpServer.Tests;

public sealed class PhcApiClientTests
{
    [Fact]
    public async Task Orders_EncodeAllFiltersAndMapResponse()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("""
            {"error":false,"msg":"OK","data":[{"orderId":"o1","documentType":1,
            "documentName":"Order","orderNumber":12,"year":2026,"orderDate":null,
            "entityNumber":2,"establishment":0,"entityName":"Test","isClosed":false,"totalAmount":12.5}]}
            """));
        using var client = CreateClient(handler, clock, "test-key");
        var result = await client.GetEncomendasAsync(12, new(2026, 2, "A&B + C", "2026-01-01", "2026-12-31", false));
        var row = Assert.Single(result.Data!);
        Assert.Equal("o1", row.OrderId);
        Assert.Equal(12.5, row.DocumentTotal);
        Assert.Null(row.OrderDate);
        Assert.Equal("https://phc.example/PhcApi/api/comercial/encomendas?year=2026&clientNumber=2&clientName=A%26B%20%2B%20C&startDate=2026-01-01&endDate=2026-12-31&isClosed=false&orderNumber=12",
            handler.Requests[1].Url);
        Assert.Equal("test-key", handler.Requests[1].ApiKey);
        Assert.NotNull(handler.Requests[1].Authorization);
    }

    [Fact]
    public async Task OrderDetails_RepeatArrayKeysAndMapLines()
    {
        var clock = new TestClock();
        using var response = Json("""
            {"error":false,"data":[{"orderId":"o1","orderNumber":12,"lines":[
            {"lineId":"l1","lineOrder":1.5,"reference":"REF","description":null,
            "orderedQuantity":5,"fulfilledQuantity":2,"pendingQuantity":3,"lineTotal":42.5}]}]}
            """);
        response.Content.Headers.ContentType = new("text/plain");
        using var handler = new RecordingHandler(Login(clock), response);
        using var client = CreateClient(handler, clock);
        var result = await client.GetEncomendasDetalheAsync([12, 13], new(IsClosed: true));
        Assert.EndsWith("/api/comercial/encomendas/detalhe?isClosed=true&orderNumbers=12&orderNumbers=13", handler.Requests[1].Url);
        var line = Assert.Single(Assert.Single(result.Data!).Lines!);
        Assert.Equal("l1", line.LineId);
        Assert.Equal(3, line.PendingQuantity);
        Assert.Equal(42.5, line.LineTotal);
    }

    [Fact]
    public async Task MissingFilters_AreNotSent_AndUsersUseRest()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock),
            Json("""{"error":false,"data":null}"""),
            Json("""{"error":false,"data":[]}"""),
            Json("""{"error":false,"data":[{"UserNumber":2.5,"UserCode":"test","UserName":null,"Email":null}]}"""));
        using var client = CreateClient(handler, clock);
        Assert.Null((await client.GetEncomendasAsync(null, new())).Data);
        Assert.Empty((await client.GetEncomendasDetalheAsync([], new())).Data!);
        var user = Assert.Single((await client.GetUtilizadoresAsync()).Data!);
        Assert.Equal(2.5, user.UserNumber);
        Assert.Null(user.Email);
        Assert.EndsWith("/api/comercial/encomendas", handler.Requests[1].Url);
        Assert.EndsWith("/api/comercial/encomendas/detalhe", handler.Requests[2].Url);
        Assert.EndsWith("/api/utilizadores", handler.Requests[3].Url);
    }

    [Fact]
    public async Task Invoice_WrapsOptionsWithExactCasingAndMapsResult()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("""
            {"error":false,"msg":"Created","data":{"client":2,"establishment":0,"clientName":"Test",
            "ftstamp":"f1","documentNumber":42,"documentDate":"2026-10-01T00:00:00",
            "documentName":"Invoice","documentTotal":123.45,"documentAtCud":"cud",
            "documentAtDocNumber":"doc","pdfDocumentType":"PDF","pdfBinary":"YWJj",
            "linesInvoiced":1,"orders":[{"order":"o1","linesRequested":2,"linesInvoiced":1,
            "linesNotFound":["missing"],"orderClosed":true}]}}
            """));
        using var client = CreateClient(handler, clock, "test-key");
        var result = await client.CriarFaturaAsync(new()
        {
            Options = new()
            {
                GetPDFPrintOut = 1, CloseOrder = true,
                Orders = [new() { OrderID = "o1", OrderLineID = ["l1"] }, new() { OrderID = "o2" }]
            }
        });
        var request = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/api/comercial/faturas/de-encomendas", request.Url);
        using var json = JsonDocument.Parse(request.Body!);
        var options = json.RootElement.GetProperty("options");
        Assert.Equal(1, options.GetProperty("getPDFPrintOut").GetInt32());
        Assert.True(options.GetProperty("closeOrder").GetBoolean());
        Assert.Equal("o1", options.GetProperty("orders")[0].GetProperty("OrderID").GetString());
        Assert.Equal("l1", options.GetProperty("orders")[0].GetProperty("OrderLineID")[0].GetString());
        Assert.False(options.GetProperty("orders")[1].TryGetProperty("OrderLineID", out _));
        Assert.Equal("f1", result.Data!.Ftstamp);
        Assert.Equal(123.45, result.Data.DocumentTotal);
        Assert.Equal("YWJj", result.Data.PdfBinary);
        Assert.Equal("missing", Assert.Single(Assert.Single(result.Data.Orders!).LinesNotFound!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public async Task InvalidYear_DoesNotContactApi(int year)
    {
        using var handler = new RecordingHandler();
        using var client = CreateClient(handler, new TestClock());
        await Assert.ThrowsAsync<PhcApiException>(() => client.GetEncomendasAsync(null, new(Year: year)));
        await Assert.ThrowsAsync<PhcApiException>(() => client.GetEncomendasDetalheAsync(null, new(Year: year)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task InvalidInvoice_DoesNotContactApi()
    {
        using var handler = new RecordingHandler();
        using var client = CreateClient(handler, new TestClock());
        var requests = new CriarFaturaPedidoDto[]
        {
            new() { Options = null! },
            new() { Options = new() { Orders = [] } },
            new() { Options = new() { Orders = null! } },
            new() { Options = new() { Orders = [null!] } },
            new() { Options = new() { Orders = [new() { OrderID = "" }] } },
            new() { Options = new() { Orders = [new() { OrderID = "o1" }], GetPDFPrintOut = 2 } },
            new() { Options = new() { Orders = [new() { OrderID = "o1" }], GetPDFPrintOut = -1 } }
        };
        foreach (var request in requests)
            await Assert.ThrowsAsync<PhcApiException>(() => client.CriarFaturaAsync(request));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("""{"error":true,"msg":"Cannot invoice","data":null}""")]
    [InlineData("""{"error":false,"data":{"documentNumber":"invalid"}}""")]
    [InlineData("""{}""")]
    [InlineData("""null""")]
    [InlineData("not-json")]
    public async Task Invoice_BusinessAndPayloadErrorsAreNotReplayed(string payload)
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json(payload));
        using var client = CreateClient(handler, clock);
        var error = await Assert.ThrowsAsync<PhcApiException>(() => client.CriarFaturaAsync(new()
        {
            Options = new() { Orders = [new() { OrderID = "o1" }] }
        }));
        Assert.Contains("retrying", error.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Invoice_HttpFailureIsNeverReplayed(HttpStatusCode status)
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("private-error", status));
        using var client = CreateClient(handler, clock);
        var error = await Assert.ThrowsAsync<PhcApiException>(() => client.CriarFaturaAsync(new()
        {
            Options = new() { Orders = [new() { OrderID = "o1" }] }
        }));
        Assert.DoesNotContain("private-error", error.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Invoice_TransportFailureIsNeverReplayed(bool timeout)
    {
        var clock = new TestClock();
        using var handler = new InvoiceFailureHandler(Login(clock), timeout);
        using var client = new PhcApiClient(new TestHttpClientFactory(handler),
            Options.Create(new PhcApiOptions
            {
                BaseUrl = "https://phc.example/PhcApi/",
                Username = "test-user",
                Password = "test-password"
            }), clock, NullLogger<PhcApiClient>.Instance);
        var error = await Assert.ThrowsAsync<PhcApiException>(() => client.CriarFaturaAsync(new()
        {
            Options = new() { Orders = [new() { OrderID = "o1" }] }
        }));
        Assert.Contains("confirm execution status", error.Message);
        Assert.Equal(2, handler.Calls);
    }

    private sealed class InvoiceFailureHandler(HttpResponseMessage login, bool timeout) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (request.RequestUri!.AbsolutePath.EndsWith("/login"))
                return Task.FromResult(login);
            Assert.EndsWith("/api/comercial/faturas/de-encomendas", request.RequestUri.AbsolutePath);
            if (timeout)
                throw new TaskCanceledException();
            throw new HttpRequestException();
        }
    }

    [Fact]
    public async Task Reports_UseApplicationRootAndReuseBearerToken()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("""{"rows":[]}"""), Json("[]"));
        using var client = CreateClient(handler, clock, apiKey: "test-key");

        var balance = await client.GetBalanceteAsync(2026, 9);
        await client.GetFluxoCaixaAsync(null, null);

        Assert.Equal(JsonValueKind.Array, balance.GetProperty("rows").ValueKind);
        Assert.Equal(3, handler.Requests.Count);
        var login = handler.Requests[0];
        Assert.Equal("https://phc.example/PhcApi/login", login.Url);
        Assert.Equal(HttpMethod.Post, login.Method);
        using var body = JsonDocument.Parse(login.Body!);
        Assert.Equal("test-user", body.RootElement.GetProperty("username").GetString());
        Assert.Equal("test-password", body.RootElement.GetProperty("password").GetString());
        Assert.Null(login.Authorization);
        Assert.All(handler.Requests, r => Assert.Equal("test-key", r.ApiKey));
        Assert.Equal("https://phc.example/PhcApi/api/contabilidade/balancete?ano=2026&mes=9", handler.Requests[1].Url);
        Assert.Equal("https://phc.example/PhcApi/api/contabilidade/fluxo-caixa", handler.Requests[2].Url);
        Assert.Equal("Bearer test-token", handler.Requests[1].Authorization);
    }

    [Fact]
    public async Task Scripts_SendExactPayloadWithoutApiKeyWhenNotConfigured()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("""{"ok":true}"""));
        using var client = CreateClient(handler, clock);
        using var parameters = JsonDocument.Parse("""{"id":12,"values":[true,null]}""");
        await client.ExecutarScriptAsync(new("test-expression", "test-summary", parameters.RootElement));

        var request = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://phc.example/PhcApi/api/scripts/executar", request.Url);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("test-expression", body.RootElement.GetProperty("expressao").GetString());
        Assert.Equal("test-summary", body.RootElement.GetProperty("resumo").GetString());
        Assert.Equal(12, body.RootElement.GetProperty("parametros").GetProperty("id").GetInt32());
        Assert.All(handler.Requests, r => Assert.Null(r.ApiKey));
    }

    [Fact]
    public async Task ExpiringToken_IsRenewed()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("{}"), Login(clock, "new-token", 120), Json("{}"));
        using var client = CreateClient(handler, clock);
        await client.GetHealthAsync();
        clock.Now = clock.Now.AddMinutes(59).AddSeconds(45);
        await client.GetHealthAsync();

        Assert.Equal(2, handler.Requests.Count(r => r.Url.EndsWith("/login")));
        Assert.Equal("Bearer new-token", handler.Requests.Last().Authorization);
    }

    [Fact]
    public async Task ConcurrentRequests_ShareOneLogin()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(
            Login(clock), Json("{}"), Json("{}"), Json("{}"), Json("{}"));
        using var client = CreateClient(handler, clock);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => client.GetHealthAsync()));
        Assert.Single(handler.Requests, r => r.Url.EndsWith("/login"));
    }

    [Fact]
    public async Task Script401_IsNotReplayedAndInvalidatesTokenForNextCall()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(
            Login(clock), Json("private-upstream-error", HttpStatusCode.Unauthorized),
            Login(clock, "renewed-token"), Json("{}"));
        using var client = CreateClient(handler, clock);
        var error = await Assert.ThrowsAsync<PhcApiException>(() =>
            client.ExecutarScriptAsync(new("test-expression", null, null)));

        Assert.Equal(2, handler.Requests.Count);
        Assert.Contains("PhcApi:ApiKey", error.Message);
        Assert.DoesNotContain("private-upstream-error", error.Message);
        await client.GetHealthAsync();
        Assert.Equal("Bearer renewed-token", handler.Requests.Last().Authorization);
        Assert.Single(handler.Requests, r => r.Url.EndsWith("/api/scripts/executar"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Redirect)]
    public async Task ScriptHttpErrors_AreNotRetried(HttpStatusCode status)
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("private-error", status));
        using var client = CreateClient(handler, clock);
        var error = await Assert.ThrowsAsync<PhcApiException>(() =>
            client.ExecutarScriptAsync(new("test-expression", null, null)));
        Assert.Contains(((int)status).ToString(), error.Message);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"token":"test-token","expiresAtUtc":"2020-01-01T00:00:00Z"}""")]
    [InlineData("""{"token":"","expiresAtUtc":"2099-01-01T00:00:00Z"}""")]
    [InlineData("not-json")]
    public async Task InvalidLogin_DoesNotSendBusinessRequest(string response)
    {
        using var handler = new RecordingHandler(Json(response));
        using var client = CreateClient(handler, new TestClock());
        await Assert.ThrowsAsync<PhcApiException>(() => client.GetHealthAsync());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Login401_IsReportedWithoutSendingBusinessRequest()
    {
        using var handler = new RecordingHandler(Json("private-error", HttpStatusCode.Unauthorized));
        using var client = CreateClient(handler, new TestClock());
        var error = await Assert.ThrowsAsync<PhcApiException>(() => client.GetHealthAsync());
        Assert.Contains("'login'", error.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Health_CanReturnPlainText()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Healthy", Encoding.UTF8, "text/plain")
        });
        using var client = CreateClient(handler, clock);
        var response = await client.GetHealthAsync();
        Assert.Equal("Healthy", response.GetString());
        Assert.Equal("https://phc.example/PhcApi/health", handler.Requests[1].Url);
    }

    [Fact]
    public async Task Report_InvalidJsonIsReportedWithoutLeakingBody()
    {
        var clock = new TestClock();
        using var handler = new RecordingHandler(Login(clock), Json("private-response"));
        using var client = CreateClient(handler, clock);
        var error = await Assert.ThrowsAsync<PhcApiException>(() => client.GetBalanceteAsync(null, 9));
        Assert.DoesNotContain("private-response", error.Message);
        Assert.EndsWith("?mes=9", handler.Requests[1].Url);
    }

    [Fact]
    public async Task InvalidScript_IsRejectedBeforeLogin()
    {
        using var handler = new RecordingHandler();
        using var client = CreateClient(handler, new TestClock());
        await Assert.ThrowsAsync<PhcApiException>(() => client.ExecutarScriptAsync(new("", null, null)));
        await Assert.ThrowsAsync<PhcApiException>(() => client.ExecutarScriptAsync(new("test", new string('x', 121), null)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Cancellation_PropagatesWithoutAuthentication()
    {
        using var handler = new RecordingHandler();
        using var client = CreateClient(handler, new TestClock());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetHealthAsync(cts.Token));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://phc.example/PhcApi/")]
    [InlineData("https://phc.example/PhcApi/?token=secret")]
    public async Task InvalidBaseUrl_FailsBeforeSendingCredentials(string baseUrl)
    {
        using var handler = new RecordingHandler();
        using var client = new PhcApiClient(new TestHttpClientFactory(handler),
            Options.Create(new PhcApiOptions
            {
                BaseUrl = baseUrl,
                Username = "test-user",
                Password = "test-password"
            }), new TestClock(), NullLogger<PhcApiClient>.Instance);
        await Assert.ThrowsAsync<PhcApiException>(() => client.GetHealthAsync());
        Assert.Empty(handler.Requests);
    }

    private static PhcApiClient CreateClient(RecordingHandler handler, TestClock clock, string? apiKey = null) =>
        new(new TestHttpClientFactory(handler), Options.Create(new PhcApiOptions
        {
            BaseUrl = "https://phc.example/PhcApi",
            Username = "test-user",
            Password = "test-password",
            ApiKey = apiKey
        }), clock, NullLogger<PhcApiClient>.Instance);

    private static HttpResponseMessage Login(TestClock clock, string token = "test-token", int minutes = 60) =>
        Json($$"""{"token":"{{token}}","expiresAtUtc":"{{clock.Now.AddMinutes(minutes):O}}"}""");

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record RecordedRequest(HttpMethod Method, string Url, string? Authorization, string? ApiKey, string? Body);

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            lock (_responses)
            {
                Requests.Add(new(request.Method, request.RequestUri!.AbsoluteUri,
                    request.Headers.Authorization?.ToString(),
                    request.Headers.TryGetValues("X-API-Key", out var keys) ? keys.Single() : null, body));
                return _responses.Dequeue();
            }
        }
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(PhcApiClient.HttpClientName, name);
            return new(handler, disposeHandler: false);
        }
    }
}
