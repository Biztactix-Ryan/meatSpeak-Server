namespace MeatSpeak.Server.AdminApi.JsonRpc;

using System.Text.Json.Serialization;

public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }

    [JsonPropertyName("id")]
    public System.Text.Json.JsonElement? Id { get; set; }

    public static JsonRpcResponse Success(object? result, System.Text.Json.JsonElement? id) => new()
    {
        Result = result,
        Id = id,
    };

    public static JsonRpcResponse Failure(JsonRpcError error, System.Text.Json.JsonElement? id) => new()
    {
        Error = error,
        Id = id,
    };
}
