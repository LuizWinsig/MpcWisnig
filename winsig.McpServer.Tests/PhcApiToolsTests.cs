using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using winsig.McpServer.Api;
using winsig.McpServer.Models;
using winsig.McpServer.Tools;

namespace winsig.McpServer.Tests;

public sealed class PhcApiToolsTests
{
    [Fact]
    public async Task Reader_CanReadAllReportsAndHealth()
    {
        var client = new StubClient();
        var tools = CreateTools(client, "Phc.Read");
        await tools.GetBalanceteAsync(2026, 9);
        await tools.GetFluxoCaixaAsync(2026, 9);
        await tools.GetHealthAsync();
        await tools.GetEncomendasAsync();
        await tools.GetEncomendasDetalheAsync();
        await tools.GetUtilizadoresAsync();
        Assert.Equal(6, client.Calls);
        await Assert.ThrowsAsync<McpException>(() => tools.CriarFaturaAsync(new() { Orders = [] }));
        Assert.Equal(6, client.Calls);
    }

    [Fact]
    public async Task Admin_CanCreateInvoice()
    {
        var client = new StubClient();
        await CreateTools(client, "Phc.Admin").CriarFaturaAsync(new()
        {
            Orders = [new() { OrderID = "order-1" }]
        });
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public void ScriptExecution_IsNotExposedAsAnMcpTool()
    {
        var names = typeof(PhcApiTools).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: true))
            .Cast<McpServerToolAttribute>()
            .Select(attribute => attribute.Name);
        Assert.DoesNotContain("executar_script", names);
        Assert.Null(typeof(PhcApiTools).GetMethod("ExecutarScriptAsync"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Phc.ReadDetail")]
    public async Task MissingRole_DeniesAllToolsBeforeContactingApi(string? role)
    {
        var client = new StubClient();
        var tools = CreateTools(client, role);
        await Assert.ThrowsAsync<McpException>(() => tools.GetBalanceteAsync());
        await Assert.ThrowsAsync<McpException>(() => tools.GetFluxoCaixaAsync());
        await Assert.ThrowsAsync<McpException>(() => tools.GetHealthAsync());
        await Assert.ThrowsAsync<McpException>(() => tools.GetEncomendasAsync());
        await Assert.ThrowsAsync<McpException>(() => tools.GetEncomendasDetalheAsync());
        await Assert.ThrowsAsync<McpException>(() => tools.GetUtilizadoresAsync());
        await Assert.ThrowsAsync<McpException>(() => tools.CriarFaturaAsync(new() { Orders = [] }));
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task ApiError_BecomesMcpToolError()
    {
        var client = new StubClient { Error = new PhcApiException("PHC API returned HTTP 401.") };
        var error = await Assert.ThrowsAsync<McpException>(() => CreateTools(client, "Phc.Read").GetHealthAsync());
        Assert.Equal(client.Error.Message, error.Message);
    }

    [Fact]
    public void Tools_CanGenerateSchemasWithSourceGeneratedContext()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions);
        options.TypeInfoResolverChain.Add(PhcJsonContext.Default);
        var tools = CreateTools(new StubClient(), "Phc.Read");
        foreach (var name in new[] { "GetBalanceteAsync", "GetFluxoCaixaAsync", "GetHealthAsync",
            "GetEncomendasAsync", "GetEncomendasDetalheAsync", "GetUtilizadoresAsync", "CriarFaturaAsync" })
        {
            var tool = McpServerTool.Create(typeof(PhcApiTools).GetMethod(name)!, tools,
                new McpServerToolCreateOptions { SerializerOptions = options });
            Assert.Equal(JsonValueKind.Object, tool.ProtocolTool.InputSchema.ValueKind);
            if (name == "CriarFaturaAsync")
            {
                Assert.False(tool.ProtocolTool.Annotations!.ReadOnlyHint);
                Assert.True(tool.ProtocolTool.Annotations.DestructiveHint);
                Assert.False(tool.ProtocolTool.Annotations.IdempotentHint);
                Assert.Contains(tool.ProtocolTool.InputSchema.GetProperty("required").EnumerateArray(),
                    value => value.GetString() == "options");
                var schema = tool.ProtocolTool.InputSchema.GetRawText();
                Assert.Contains("\"OrderID\"", schema);
                Assert.Contains("\"getPDFPrintOut\"", schema);
            }

        }
    }

    [Fact]
    public void OrderNumbers_HaveSourceGeneratedMetadata()
    {
        var typeInfo = PhcJsonContext.Default.GetTypeInfo(typeof(int[]));
        Assert.NotNull(typeInfo);
        Assert.Equal("[12,13]", JsonSerializer.Serialize(new[] { 12, 13 }, typeInfo));
    }

    private static PhcApiTools CreateTools(StubClient client, string? role)
    {
        var identity = role is null ? new ClaimsIdentity() :
            new ClaimsIdentity([new Claim("roles", role)], "test", "sub", "roles");
        return new(client, new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        }, NullLogger<PhcApiTools>.Instance);
    }

    private sealed class StubClient : IPhcApiClient
    {
        public int Calls { get; private set; }
        public PhcApiException? Error { get; init; }
        private Task<JsonElement> Result()
        {
            Calls++;
            if (Error is not null)
                throw Error;
            using var document = JsonDocument.Parse("{}");
            return Task.FromResult(document.RootElement.Clone());
        }
        public Task<JsonElement> GetBalanceteAsync(int? ano, int? mes, CancellationToken ct = default) => Result();
        public Task<JsonElement> GetFluxoCaixaAsync(int? ano, int? mes, CancellationToken ct = default) => Result();
        public Task<JsonElement> GetHealthAsync(CancellationToken ct = default) => Result();
        private Task<PhcEnvelope<T>> Envelope<T>()
        {
            Calls++;
            if (Error is not null)
                throw Error;
            return Task.FromResult(new PhcEnvelope<T>());
        }
        public Task<PhcEnvelope<List<DocumentoAbertoDto>>> GetEncomendasAsync(int? orderNumber, ComercialQuery filters, CancellationToken ct = default) => Envelope<List<DocumentoAbertoDto>>();
        public Task<PhcEnvelope<List<EncomendaDetalheDto>>> GetEncomendasDetalheAsync(int[]? orderNumbers, ComercialQuery filters, CancellationToken ct = default) => Envelope<List<EncomendaDetalheDto>>();
        public Task<PhcEnvelope<CriarFaturaResultadoDto>> CriarFaturaAsync(CriarFaturaPedidoDto request, CancellationToken ct = default) => Envelope<CriarFaturaResultadoDto>();
        public Task<PhcEnvelope<List<UtilizadorDto>>> GetUtilizadoresAsync(CancellationToken ct = default) => Envelope<List<UtilizadorDto>>();
        public Task<JsonElement> ExecutarScriptAsync(ExecutarScriptRequest request, CancellationToken ct = default) => Result();
    }
}
