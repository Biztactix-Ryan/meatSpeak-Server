namespace MeatSpeak.Server.Core.Commands;

using Microsoft.Extensions.Logging;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, ICommandHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<CommandRegistry> _logger;

    public CommandRegistry(ILogger<CommandRegistry> logger)
    {
        _logger = logger;
    }

    public void Register(ICommandHandler handler)
    {
        if (_handlers.TryAdd(handler.Command, handler))
            _logger.LogDebug("Registered command handler: {Command}", handler.Command);
        else
            _logger.LogWarning("Duplicate command handler for {Command}, keeping first", handler.Command);
    }

    public ICommandHandler? Resolve(string command) =>
        _handlers.TryGetValue(command, out var handler) ? handler : null;

    public IReadOnlyDictionary<string, ICommandHandler> All => _handlers;
}
