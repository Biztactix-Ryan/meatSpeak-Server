namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;

internal static class AdminParamHelper
{
    public static JsonElement Require(JsonElement? parameters)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");
        return parameters.Value;
    }

    public static string RequireString(JsonElement p, string name)
    {
        return p.GetProperty(name).GetString()
            ?? throw new JsonException($"Missing '{name}'");
    }

    public static Guid RequireGuid(JsonElement p, string name)
    {
        var str = RequireString(p, name);
        if (!Guid.TryParse(str, out var id))
            throw new JsonException($"Invalid '{name}' format");
        return id;
    }

    public static string? OptionalString(JsonElement p, string name)
    {
        return p.TryGetProperty(name, out var el) ? el.GetString() : null;
    }
}
