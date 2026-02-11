namespace MeatSpeak.Server.Admin;

using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.AdminApi.JsonRpc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class AdminApiMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JsonRpcProcessor _processor;
    private readonly ApiKeyAuthenticator _authenticator;
    private readonly ILogger<AdminApiMiddleware> _logger;

    public AdminApiMiddleware(
        RequestDelegate next,
        JsonRpcProcessor processor,
        ApiKeyAuthenticator authenticator,
        ILogger<AdminApiMiddleware> logger)
    {
        _next = next;
        _processor = processor;
        _authenticator = authenticator;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Method != "POST")
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            context.Response.Headers["Allow"] = "POST";
            return;
        }

        // Extract Bearer token
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var errorJson = System.Text.Json.JsonSerializer.Serialize(
                JsonRpcResponse.Failure(JsonRpcError.Unauthorized, null));
            await context.Response.WriteAsync(errorJson);
            return;
        }

        var apiKey = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            var errorJson = System.Text.Json.JsonSerializer.Serialize(
                JsonRpcResponse.Failure(JsonRpcError.Unauthorized, null));
            await context.Response.WriteAsync(errorJson);
            return;
        }

        // Read the JSON body
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        // Parse the request to get the method name for permission checking
        string? methodName = null;
        try
        {
            var request = System.Text.Json.JsonSerializer.Deserialize<JsonRpcRequest>(body);
            methodName = request?.Method;
        }
        catch
        {
            // Let the processor handle parse errors
        }

        // Authenticate with method-level check
        if (!_authenticator.Authenticate(apiKey, methodName))
        {
            _logger.LogWarning("Failed API key authentication from {RemoteIp}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var errorJson = System.Text.Json.JsonSerializer.Serialize(
                JsonRpcResponse.Failure(JsonRpcError.Forbidden, null));
            await context.Response.WriteAsync(errorJson);
            return;
        }

        // Process JSON-RPC
        var result = await _processor.ProcessRawAsync(body, context.RequestAborted);
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(result);
    }
}
