using System.Net;
using System.Text;
using Xunit;
using MeatSpeak.Server.Admin;
using MeatSpeak.Server.AdminApi;
using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.AdminApi.JsonRpc;
using MeatSpeak.Server.AdminApi.Methods;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace MeatSpeak.Server.Tests;

public class AdminApiMiddlewareTests
{
    private const string TestApiKey = "test-secret-key-12345";
    private static readonly string TestKeyHash = ApiKeyAuthenticator.GenerateHash(TestApiKey);

    private readonly JsonRpcProcessor _processor;
    private readonly ApiKeyAuthenticator _authenticator;

    public AdminApiMiddlewareTests()
    {
        var methods = new IAdminMethod[] { new EchoMethod() };
        _processor = new JsonRpcProcessor(methods, NullLogger<JsonRpcProcessor>.Instance);
        _authenticator = new ApiKeyAuthenticator(new[]
        {
            new ApiKeyEntry { Name = "test", KeyHash = TestKeyHash }
        });
    }

    private AdminApiMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new AdminApiMiddleware(next, _processor, _authenticator,
            NullLogger<AdminApiMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateContext(string method, string path, string? body = null, string? authHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Loopback;

        if (authHeader != null)
            context.Request.Headers.Authorization = authHeader;

        if (body != null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            context.Request.Body = new MemoryStream(bodyBytes);
            context.Request.ContentType = "application/json";
        }

        context.Response.Body = new MemoryStream();

        return context;
    }

    private static string ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }

    [Fact]
    public async Task InvokeAsync_NonApiPath_PassesThrough()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("GET", "/other");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_GetRequest_Returns405()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("GET", "/api");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(405, context.Response.StatusCode);
        Assert.Equal("POST", context.Response.Headers["Allow"].ToString());
    }

    [Fact]
    public async Task InvokeAsync_NoAuthHeader_Returns401()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("POST", "/api", body: "{}");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
        var body = ReadResponseBody(context);
        Assert.Contains("-32000", body); // Unauthorized error code
    }

    [Fact]
    public async Task InvokeAsync_EmptyBearerToken_Returns401()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("POST", "/api", body: "{}", authHeader: "Bearer ");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_Returns403()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var json = """{"jsonrpc":"2.0","method":"test.echo","id":1}""";
        var context = CreateContext("POST", "/api", body: json, authHeader: "Bearer wrong-key");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
        var body = ReadResponseBody(context);
        Assert.Contains("-32001", body); // Forbidden error code
    }

    [Fact]
    public async Task InvokeAsync_ValidRequest_ReturnsJsonRpcSuccess()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var json = """{"jsonrpc":"2.0","method":"test.echo","id":1}""";
        var context = CreateContext("POST", "/api", body: json, authHeader: $"Bearer {TestApiKey}");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        var body = ReadResponseBody(context);
        Assert.Contains("result", body);
        Assert.DoesNotContain("error", body);
    }

    [Fact]
    public async Task InvokeAsync_ValidAuth_UnknownMethod_ReturnsMethodNotFound()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var json = """{"jsonrpc":"2.0","method":"nonexistent.method","id":1}""";
        var context = CreateContext("POST", "/api", body: json, authHeader: $"Bearer {TestApiKey}");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        var body = ReadResponseBody(context);
        Assert.Contains("-32601", body); // Method not found
    }

    [Fact]
    public async Task InvokeAsync_ValidAuth_InvalidJson_ReturnsParseError()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("POST", "/api", body: "{bad json!!", authHeader: $"Bearer {TestApiKey}");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        var body = ReadResponseBody(context);
        Assert.Contains("-32700", body); // Parse error
    }

    [Fact]
    public async Task InvokeAsync_ApiSubpath_MatchesRoute()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("POST", "/api/v2");

        await middleware.InvokeAsync(context);

        // /api/v2 starts with /api, so middleware should handle it
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NonBearerAuth_Returns401()
    {
        bool nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = CreateContext("POST", "/api", body: "{}", authHeader: "Basic dXNlcjpwYXNz");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(401, context.Response.StatusCode);
    }

    private sealed class EchoMethod : IAdminMethod
    {
        public string Name => "test.echo";
        public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
            => Task.FromResult<object?>(new { echo = "ok" });
    }
}
