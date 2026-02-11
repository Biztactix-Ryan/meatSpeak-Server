namespace MeatSpeak.Server.AdminApi.JsonRpc;

using System.Text.Json;
using MeatSpeak.Server.AdminApi.Methods;
using Microsoft.Extensions.Logging;

public sealed class JsonRpcProcessor
{
    private readonly Dictionary<string, IAdminMethod> _methods = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<JsonRpcProcessor> _logger;

    public JsonRpcProcessor(IEnumerable<IAdminMethod> methods, ILogger<JsonRpcProcessor> logger)
    {
        _logger = logger;
        foreach (var method in methods)
            _methods[method.Name] = method;
    }

    public void RegisterMethod(IAdminMethod method)
    {
        _methods[method.Name] = method;
    }

    public async Task<JsonRpcResponse> ProcessAsync(JsonRpcRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Method))
            return JsonRpcResponse.Failure(JsonRpcError.InvalidRequest, request.Id);

        if (!_methods.TryGetValue(request.Method, out var method))
        {
            _logger.LogDebug("Unknown method: {Method}", request.Method);
            return JsonRpcResponse.Failure(JsonRpcError.MethodNotFound, request.Id);
        }

        try
        {
            var result = await method.ExecuteAsync(request.Params, ct);
            return JsonRpcResponse.Success(result, request.Id);
        }
        catch (JsonException)
        {
            return JsonRpcResponse.Failure(JsonRpcError.InvalidParams, request.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing method {Method}", request.Method);
            return JsonRpcResponse.Failure(JsonRpcError.InternalError, request.Id);
        }
    }

    public async Task<string> ProcessRawAsync(string json, CancellationToken ct = default)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(json);
            if (request == null)
                return JsonSerializer.Serialize(JsonRpcResponse.Failure(JsonRpcError.ParseError, null));

            var response = await ProcessAsync(request, ct);
            return JsonSerializer.Serialize(response);
        }
        catch (JsonException)
        {
            return JsonSerializer.Serialize(JsonRpcResponse.Failure(JsonRpcError.ParseError, null));
        }
    }

    public IReadOnlyDictionary<string, IAdminMethod> Methods => _methods;
}
