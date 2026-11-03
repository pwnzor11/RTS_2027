namespace Rts.Game.Sandbox;

/// <summary>A unit spawned at setup, with its owner, so scripted orders can pick the right player.</summary>
public readonly record struct OwnedUnit(UnitKey Key, byte Player);
