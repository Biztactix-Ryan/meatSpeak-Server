using MeatSpeak.Server;
using MeatSpeak.Server.Config;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Events;
using MeatSpeak.Server.Numerics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.State;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.Handlers.ServerInfo;
using MeatSpeak.Server.Handlers.Operator;
using MeatSpeak.Server.Handlers.Voice;
using MeatSpeak.Server.Handlers.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Load config
var configPath = Path.Combine(AppContext.BaseDirectory, "Config", "server-config.json");
var config = ServerConfigLoader.Load(configPath);

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

// Hosted service
builder.Services.AddHostedService<ServerHost>();

var host = builder.Build();

// Register command handlers after build
var server = host.Services.GetRequiredService<IServer>();
var registration = host.Services.GetRequiredService<RegistrationPipeline>();
var numerics = host.Services.GetRequiredService<NumericSender>();

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

await host.RunAsync();
