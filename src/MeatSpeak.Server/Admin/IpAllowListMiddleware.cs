namespace MeatSpeak.Server.Admin;

using System.Net;
using MeatSpeak.Server.Core.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public sealed class IpAllowListMiddleware
{
    private readonly RequestDelegate _next;
    private readonly List<(IPAddress Network, int PrefixLength)> _allowedNetworks;
    private readonly ILogger<IpAllowListMiddleware> _logger;

    private static readonly string[] ProtectedPrefixes = { "/api", "/admin" };

    public IpAllowListMiddleware(RequestDelegate next, ServerConfig config, ILogger<IpAllowListMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _allowedNetworks = ParseCidrs(config.AdminApi.IpAllowList);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        bool isProtected = false;
        foreach (var prefix in ProtectedPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                isProtected = true;
                break;
            }
        }

        if (!isProtected)
        {
            await _next(context);
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        // Normalize IPv4-mapped IPv6 to IPv4
        if (remoteIp.IsIPv4MappedToIPv6)
            remoteIp = remoteIp.MapToIPv4();

        if (!IsAllowed(remoteIp))
        {
            _logger.LogWarning("Blocked admin access from {RemoteIp} to {Path}", remoteIp, path);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await _next(context);
    }

    private bool IsAllowed(IPAddress remoteIp)
    {
        foreach (var (network, prefixLength) in _allowedNetworks)
        {
            if (IsInNetwork(remoteIp, network, prefixLength))
                return true;
        }
        return false;
    }

    private static bool IsInNetwork(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();

        if (addressBytes.Length != networkBytes.Length)
            return false;

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != networkBytes[i])
                return false;
        }

        if (remainingBits > 0 && fullBytes < addressBytes.Length)
        {
            int mask = 0xFF << (8 - remainingBits);
            if ((addressBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask))
                return false;
        }

        return true;
    }

    private static List<(IPAddress Network, int PrefixLength)> ParseCidrs(List<string> cidrs)
    {
        var result = new List<(IPAddress, int)>();

        if (cidrs.Count == 0)
        {
            // Default: localhost only
            result.Add((IPAddress.Loopback, 32));        // 127.0.0.1/32
            result.Add((IPAddress.IPv6Loopback, 128));   // ::1/128
            return result;
        }

        foreach (var cidr in cidrs)
        {
            var parts = cidr.Split('/');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var address) && int.TryParse(parts[1], out var prefix))
            {
                result.Add((address, prefix));
            }
            else if (IPAddress.TryParse(cidr, out var singleAddr))
            {
                // Treat bare IP as /32 or /128
                result.Add((singleAddr, singleAddr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32));
            }
        }

        return result;
    }
}
