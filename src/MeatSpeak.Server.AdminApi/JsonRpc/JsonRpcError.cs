namespace MeatSpeak.Server.AdminApi.JsonRpc;

using System.Text.Json.Serialization;

public sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }

    public static readonly JsonRpcError ParseError = new() { Code = -32700, Message = "Parse error" };
    public static readonly JsonRpcError InvalidRequest = new() { Code = -32600, Message = "Invalid Request" };
    public static readonly JsonRpcError MethodNotFound = new() { Code = -32601, Message = "Method not found" };
    public static readonly JsonRpcError InvalidParams = new() { Code = -32602, Message = "Invalid params" };
    public static readonly JsonRpcError InternalError = new() { Code = -32603, Message = "Internal error" };
    public static readonly JsonRpcError Unauthorized = new() { Code = -32000, Message = "Unauthorized" };
    public static readonly JsonRpcError Forbidden = new() { Code = -32001, Message = "Forbidden" };
}
