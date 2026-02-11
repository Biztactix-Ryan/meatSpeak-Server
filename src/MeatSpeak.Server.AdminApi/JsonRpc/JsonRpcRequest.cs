namespace MeatSpeak.Server.AdminApi.JsonRpc;

using System.Text.Json.Serialization;

public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public System.Text.Json.JsonElement? Params { get; set; }

    [JsonPropertyName("id")]
    public System.Text.Json.JsonElement? Id { get; set; }
}
