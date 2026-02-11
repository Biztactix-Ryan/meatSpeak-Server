using Xunit;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MeatSpeak.Server.AdminApi.JsonRpc;
using MeatSpeak.Server.AdminApi.Methods;

namespace MeatSpeak.Server.AdminApi.Tests;

public class JsonRpcProcessorTests
{
    private readonly JsonRpcProcessor _processor;

    public JsonRpcProcessorTests()
    {
        var methods = new IAdminMethod[] { new TestMethod() };
        _processor = new JsonRpcProcessor(methods, NullLogger<JsonRpcProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_ValidMethod_ReturnsSuccess()
    {
        var request = new JsonRpcRequest
        {
            Method = "test.echo",
            Id = JsonDocument.Parse("1").RootElement,
        };
        var response = await _processor.ProcessAsync(request);
        Assert.NotNull(response.Result);
        Assert.Null(response.Error);
    }

    [Fact]
    public async Task ProcessAsync_UnknownMethod_ReturnsMethodNotFound()
    {
        var request = new JsonRpcRequest
        {
            Method = "nonexistent.method",
            Id = JsonDocument.Parse("2").RootElement,
        };
        var response = await _processor.ProcessAsync(request);
        Assert.Null(response.Result);
        Assert.NotNull(response.Error);
        Assert.Equal(-32601, response.Error!.Code);
    }

    [Fact]
    public async Task ProcessAsync_EmptyMethod_ReturnsInvalidRequest()
    {
        var request = new JsonRpcRequest { Method = "" };
        var response = await _processor.ProcessAsync(request);
        Assert.NotNull(response.Error);
        Assert.Equal(-32600, response.Error!.Code);
    }

    [Fact]
    public async Task ProcessRawAsync_ValidJson_ReturnsResult()
    {
        var json = """{"jsonrpc":"2.0","method":"test.echo","id":1}""";
        var resultJson = await _processor.ProcessRawAsync(json);
        Assert.Contains("result", resultJson);
    }

    [Fact]
    public async Task ProcessRawAsync_InvalidJson_ReturnsParseError()
    {
        var resultJson = await _processor.ProcessRawAsync("{invalid json}");
        Assert.Contains("-32700", resultJson);
    }

    [Fact]
    public void RegisterMethod_AddsToMethods()
    {
        var processor = new JsonRpcProcessor(Array.Empty<IAdminMethod>(), NullLogger<JsonRpcProcessor>.Instance);
        processor.RegisterMethod(new TestMethod());
        Assert.True(processor.Methods.ContainsKey("test.echo"));
    }

    private sealed class TestMethod : IAdminMethod
    {
        public string Name => "test.echo";
        public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
            => Task.FromResult<object?>(new { echo = "ok" });
    }
}
