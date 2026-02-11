using System.Net;
using System.Text;
using System.Text.Json;
using MeatSpeak.Server.Tls;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MeatSpeak.Server.Tests;

public class CloudflareDns01ChallengeHandlerTests
{
    private sealed class MockHttpHandler : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        public HttpResponseMessage NextResponse { get; set; } = new(HttpStatusCode.OK);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(NextResponse);
        }
    }

    private static (CloudflareDns01ChallengeHandler handler, MockHttpHandler mock) CreateHandler()
    {
        var mockHttp = new MockHttpHandler();
        var httpClient = new HttpClient(mockHttp)
        {
            BaseAddress = new Uri("https://api.cloudflare.com")
        };
        var handler = new CloudflareDns01ChallengeHandler(httpClient, "zone123", NullLogger.Instance);
        return (handler, mockHttp);
    }

    [Fact]
    public async Task PrepareAsync_CreatesDnsTxtRecord()
    {
        var (handler, mock) = CreateHandler();
        mock.NextResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                success = true,
                result = new { id = "record-abc" }
            }), Encoding.UTF8, "application/json")
        };

        await handler.PrepareAsync("example.com", "token123", "dns-txt-value");

        Assert.Single(mock.Requests);
        var req = mock.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/zones/zone123/dns_records", req.RequestUri!.ToString());

        var body = await req.Content!.ReadAsStringAsync();
        Assert.Contains("_acme-challenge.example.com", body);
        Assert.Contains("dns-txt-value", body);
        Assert.Contains("TXT", body);
    }

    [Fact]
    public async Task CleanupAsync_DeletesDnsRecord()
    {
        var (handler, mock) = CreateHandler();

        // First prepare (creates the record)
        mock.NextResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                success = true,
                result = new { id = "record-xyz" }
            }), Encoding.UTF8, "application/json")
        };
        await handler.PrepareAsync("example.com", "token456", "dns-txt-value");

        // Reset for delete
        mock.Requests.Clear();
        mock.NextResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
        };

        await handler.CleanupAsync("example.com", "token456");

        Assert.Single(mock.Requests);
        var req = mock.Requests[0];
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Contains("/zones/zone123/dns_records/record-xyz", req.RequestUri!.ToString());
    }

    [Fact]
    public async Task CleanupAsync_UnknownToken_DoesNothing()
    {
        var (handler, mock) = CreateHandler();

        await handler.CleanupAsync("example.com", "unknown-token");

        Assert.Empty(mock.Requests);
    }
}
