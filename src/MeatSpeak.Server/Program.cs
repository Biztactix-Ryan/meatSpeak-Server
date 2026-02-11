using MeatSpeak.Server;
using MeatSpeak.Server.Admin;
using MeatSpeak.Server.AdminApi;
using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.AdminApi.JsonRpc;
using MeatSpeak.Server.AdminApi.Methods;
using MeatSpeak.Server.Config;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Events;
using MeatSpeak.Server.Numerics;
using MeatSpeak.Server.Permissions;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.State;
using MeatSpeak.Server.Tls;
using MeatSpeak.Server.Transport.Tls;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.Handlers.ServerInfo;
using MeatSpeak.Server.Handlers.Operator;
using MeatSpeak.Server.Handlers.Voice;
using MeatSpeak.Server.Handlers.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Load config
var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "server-config.json");
var config = ServerConfigLoader.Load(configPath);

// Configure Kestrel endpoints
builder.WebHost.ConfigureKestrel(kestrel =>
{
    if (config.Tls.Enabled)
    {
        // WSS on TLS port
        kestrel.ListenAnyIP(config.Tls.WebSocketTlsPort, listenOptions =>
        {
            if (!string.IsNullOrEmpty(config.Tls.CertPath))
            {
                // Manual cert
                if (!string.IsNullOrEmpty(config.Tls.CertKeyPath))
                {
                    var cert = System.Security.Cryptography.X509Certificates.X509Certificate2
                        .CreateFromPemFile(config.Tls.CertPath, config.Tls.CertKeyPath);
                    var exported = cert.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx);
                    cert.Dispose();
                    cert = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(exported, null);
                    listenOptions.UseHttps(cert);
                }
                else
                {
                    listenOptions.UseHttps(config.Tls.CertPath, config.Tls.CertPassword);
                }
            }
            else if (config.Tls.AcmeEnabled)
            {
                // ACME — cert will be provided by AcmeCertificateProvider via selector
                listenOptions.UseHttps(httpsOptions =>
                {
                    httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
                    {
                        var provider = connectionContext?.Features
                            .Get<Microsoft.AspNetCore.Connections.Features.IConnectionItemsFeature>()
                            ?.Items["__certProvider"] as ICertificateProvider;
                        // Fallback: resolve from app services (set after build)
                        return AcmeCertProviderInstance?.GetCertificate();
                    };
                });
            }
        });

        // ACME HTTP-01 challenge port
        if (config.Tls.AcmeEnabled && config.Tls.AcmeChallengeType == "Http01")
        {
            kestrel.ListenAnyIP(config.Tls.AcmeHttpPort);
        }
    }
    else if (config.WebSocketEnabled)
    {
        // Plain WebSocket only when TLS is disabled
        kestrel.ListenAnyIP(config.WebSocketPort);
    }
});

// Core registries
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IEventBus, InMemoryEventBus>();
builder.Services.AddSingleton(sp =>
{
    var registry = new CommandRegistry(sp.GetRequiredService<ILogger<CommandRegistry>>());
    return registry;
});
builder.Services.AddSingleton(sp =>
{
    var registry = new ModeRegistry();
    registry.RegisterStandardModes();
    return registry;
});
builder.Services.AddSingleton<CapabilityRegistry>();

// Server state
builder.Services.AddSingleton<IServer>(sp => new ServerState(
    sp.GetRequiredService<ServerConfig>(),
    sp.GetRequiredService<CommandRegistry>(),
    sp.GetRequiredService<ModeRegistry>(),
    sp.GetRequiredService<CapabilityRegistry>(),
    sp.GetRequiredService<IEventBus>()));

// Infrastructure
builder.Services.AddSingleton<NumericSender>(sp => new NumericSender(sp.GetRequiredService<IServer>()));
builder.Services.AddSingleton<RegistrationPipeline>();
builder.Services.AddSingleton<IrcConnectionHandler>();

// Admin API — API key authenticator
var apiKeyEntries = config.AdminApi.ApiKeys.Select(k => new ApiKeyEntry
{
    Name = k.Name,
    KeyHash = k.KeyHash,
    AllowedMethods = k.AllowedMethods,
}).ToList();
builder.Services.AddSingleton(new ApiKeyAuthenticator(apiKeyEntries));

// Admin API — method registrations
builder.Services.AddSingleton<IAdminMethod, ServerStatsMethod>();
builder.Services.AddSingleton<IAdminMethod, ServerRehashMethod>();
builder.Services.AddSingleton<IAdminMethod, ServerMotdGetMethod>();
builder.Services.AddSingleton<IAdminMethod, ServerMotdSetMethod>();
builder.Services.AddSingleton<IAdminMethod, ServerShutdownMethod>();
builder.Services.AddSingleton<IAdminMethod, ServerOperSetMethod>();
builder.Services.AddSingleton<IAdminMethod, UserListMethod>();
builder.Services.AddSingleton<IAdminMethod, UserInfoMethod>();
builder.Services.AddSingleton<IAdminMethod, UserKickMethod>();
builder.Services.AddSingleton<IAdminMethod, UserMessageMethod>();
builder.Services.AddSingleton<IAdminMethod, ChannelListMethod>();
builder.Services.AddSingleton<IAdminMethod, ChannelInfoMethod>();
builder.Services.AddSingleton<IAdminMethod, ChannelTopicMethod>();
builder.Services.AddSingleton<IAdminMethod, ChannelModeMethod>();
builder.Services.AddSingleton<IAdminMethod, ChannelCreateMethod>();
builder.Services.AddSingleton<IAdminMethod, ChannelDeleteMethod>();

// Ban and Role methods require repository services — register only if available
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new BanListMethod(sp.GetRequiredService<IBanRepository>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new BanAddMethod(sp.GetRequiredService<IBanRepository>(), sp.GetService<IAuditLogRepository>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new BanRemoveMethod(sp.GetRequiredService<IBanRepository>(), sp.GetService<IAuditLogRepository>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new BanCheckMethod(sp.GetRequiredService<IBanRepository>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleListMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleGetMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleCreateMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleUpdateMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleDeleteMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleAssignMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleRevokeMethod(sp.GetRequiredService<IPermissionService>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new RoleMembersMethod(sp.GetRequiredService<IRoleRepository>()));
builder.Services.AddSingleton<IAdminMethod>(sp =>
    new AuditQueryMethod(sp.GetRequiredService<IAuditLogRepository>()));

// Admin API — JSON-RPC processor
builder.Services.AddSingleton<JsonRpcProcessor>();

// TLS / ACME registration
if (config.Tls.Enabled)
{
    if (!string.IsNullOrEmpty(config.Tls.CertPath))
    {
        // Manual certificate
        builder.Services.AddSingleton<ICertificateProvider>(
            new FileCertificateProvider(config.Tls.CertPath, config.Tls.CertKeyPath, config.Tls.CertPassword));
    }
    else if (config.Tls.AcmeEnabled)
    {
        // ACME certificate
        var acmeCertProvider = new AcmeCertificateProvider();
        AcmeCertProviderInstance = acmeCertProvider;
        builder.Services.AddSingleton<AcmeCertificateProvider>(acmeCertProvider);
        builder.Services.AddSingleton<ICertificateProvider>(acmeCertProvider);

        if (config.Tls.AcmeChallengeType == "Dns01")
        {
            builder.Services.AddSingleton<IAcmeChallengeHandler>(sp =>
                new CloudflareDns01ChallengeHandler(
                    config.Tls.CloudflareApiToken!,
                    config.Tls.CloudflareZoneId!,
                    sp.GetRequiredService<ILogger<CloudflareDns01ChallengeHandler>>()));
        }
        else
        {
            builder.Services.AddSingleton<Http01ChallengeHandler>();
            builder.Services.AddSingleton<IAcmeChallengeHandler>(sp =>
                sp.GetRequiredService<Http01ChallengeHandler>());
        }

        builder.Services.AddHostedService<AcmeService>();
    }
}

// Hosted service for TCP
builder.Services.AddHostedService<ServerHost>();

var app = builder.Build();

// Register command handlers after build
var server = app.Services.GetRequiredService<IServer>();
var registration = app.Services.GetRequiredService<RegistrationPipeline>();
var numerics = app.Services.GetRequiredService<NumericSender>();

// Connection handlers
server.Commands.Register(new PingHandler());
server.Commands.Register(new PongHandler());
server.Commands.Register(new PassHandler(server));
server.Commands.Register(new NickHandler(server, registration));
server.Commands.Register(new UserHandler(server, registration));
server.Commands.Register(new QuitHandler());
server.Commands.Register(new CapHandler(server, registration));

// Messaging handlers
server.Commands.Register(new PrivmsgHandler(server));
server.Commands.Register(new NoticeHandler(server));

// Channel handlers
server.Commands.Register(new JoinHandler(server));
server.Commands.Register(new PartHandler(server));
server.Commands.Register(new ModeHandler(server));
server.Commands.Register(new TopicHandler(server));
server.Commands.Register(new KickHandler(server));
server.Commands.Register(new NamesHandler(server));
server.Commands.Register(new ListHandler(server));
server.Commands.Register(new InviteHandler(server));
server.Commands.Register(new WhoHandler(server));
server.Commands.Register(new WhoisHandler(server));

// Server info handlers
server.Commands.Register(new MotdHandler(server, numerics));
server.Commands.Register(new LusersHandler(server, numerics));
server.Commands.Register(new VersionHandler(server));

// Operator handlers
server.Commands.Register(new OperHandler(server));
server.Commands.Register(new KillHandler(server));
server.Commands.Register(new RehashHandler(server));

// Voice handler (stub)
server.Commands.Register(new VoiceHandler());

// Auth handler (stub)
server.Commands.Register(new AuthenticateHandler());

// Wire up middleware
if (config.WebSocketEnabled || config.Tls.Enabled)
{
    app.UseWebSockets();

    // ACME HTTP-01 challenge middleware (must be before other middleware)
    if (config.Tls is { Enabled: true, AcmeEnabled: true, AcmeChallengeType: "Http01" })
    {
        var challengeHandler = app.Services.GetRequiredService<Http01ChallengeHandler>();
        app.UseMiddleware<AcmeChallengeMiddleware>(challengeHandler);
    }

    // IP allowlist (applies to /api and /admin only)
    app.UseMiddleware<IpAllowListMiddleware>();

    // Admin API endpoint at /api
    app.UseMiddleware<AdminApiMiddleware>();

    // Static files for admin frontend
    var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    if (Directory.Exists(wwwrootPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(wwwrootPath),
            RequestPath = ""
        });
    }

    // Redirect /admin to /admin/index.html
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/admin", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.Equals("/admin/", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/admin/index.html");
            return;
        }
        await next();
    });

    // WebSocket IRC transport
    var ircHandler = app.Services.GetRequiredService<IrcConnectionHandler>();
    var wsLogger = app.Services.GetRequiredService<ILogger<WebSocketMiddleware>>();
    app.UseMiddleware<WebSocketMiddleware>(ircHandler, wsLogger, config.WebSocketPath);

    if (config.Tls.Enabled)
    {
        app.Logger.LogInformation("WebSocket transport (WSS) enabled on port {Port} at path {Path}",
            config.Tls.WebSocketTlsPort, config.WebSocketPath);
    }
    else
    {
        app.Logger.LogInformation("WebSocket transport enabled on port {Port} at path {Path}",
            config.WebSocketPort, config.WebSocketPath);
    }

    app.Logger.LogInformation("Admin API available at /api, Admin UI at /admin");
}

await app.RunAsync();

// Static field for ACME cert provider access during Kestrel configuration
partial class Program
{
    internal static AcmeCertificateProvider? AcmeCertProviderInstance;
}
