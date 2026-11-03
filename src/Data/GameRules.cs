namespace Rts.Data;

/// <summary>Global constants from data/rules/game.toml.</summary>
public sealed record GameRules(int TickRate, int DefaultMapWidth, int DefaultMapHeight);
