namespace MeatSpeak.Server.Core.Modes;

public sealed record ModeDefinition(char Char, ModeType Type, string Name, bool IsCustom = false);
