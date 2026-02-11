namespace MeatSpeak.Server.Tls;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

public sealed class CloudflareDns01ChallengeHandler : IAcmeChallengeHandler
{
    private readonly HttpClient _httpClient;
    private readonly string _zoneId;
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _recordIds = new();

    private const string CloudflareApiBase = "https://api.cloudflare.com/client/v4";

    public CloudflareDns01ChallengeHandler(string apiToken, string zoneId, ILogger logger)
        : this(CreateHttpClient(apiToken), zoneId, logger)
    {
    }

    internal CloudflareDns01ChallengeHandler(HttpClient httpClient, string zoneId, ILogger logger)
    {
        _httpClient = httpClient;
        _zoneId = zoneId;
        _logger = logger;
    }

    private static HttpClient CreateHttpClient(string apiToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        return client;
    }

    public async Task PrepareAsync(string domain, string token, string keyAuth)
    {
        var recordName = $"_acme-challenge.{domain}";
        var payload = new
        {
            type = "TXT",
            name = recordName,
            content = keyAuth,
            ttl = 120
        };

        var json = JsonSerializer.Serialize(payload);
        var response = await _httpClient.PostAsync(
            $"{CloudflareApiBase}/zones/{_zoneId}/dns_records",
            new StringContent(json, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var recordId = doc.RootElement.GetProperty("result").GetProperty("id").GetString()!;

        _recordIds[token] = recordId;
        _logger.LogInformation("Created DNS TXT record {RecordName} (ID: {RecordId})", recordName, recordId);
    }

    public async Task CleanupAsync(string domain, string token)
    {
        if (!_recordIds.TryGetValue(token, out var recordId))
            return;

        try
        {
            var response = await _httpClient.DeleteAsync(
                $"{CloudflareApiBase}/zones/{_zoneId}/dns_records/{recordId}");
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Deleted DNS TXT record ID: {RecordId}", recordId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete DNS TXT record ID: {RecordId}", recordId);
        }
        finally
        {
            _recordIds.Remove(token);
        }
    }
}
