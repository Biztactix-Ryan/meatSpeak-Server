namespace MeatSpeak.Server.Config;

using System.Text.Json;
using MeatSpeak.Server.Core.Server;

public static class ServerConfigLoader
{
    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path))
            return new ServerConfig();

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<ServerConfig>(json, options) ?? new ServerConfig();
    }
}
