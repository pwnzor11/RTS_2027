namespace Rts.Sim.Core;

/// <summary>Read-only view of sim state for the renderer, taken after each Step. Never fed back into the sim.</summary>
public sealed record SimSnapshot(int Tick, int MapWidth, int MapHeight, IReadOnlyList<UnitView> Units);

/// <summary>One unit as the renderer sees it. Facing is 0..255 (0 = north, clockwise).</summary>
public readonly record struct UnitView(EntityId Id, byte Player, int TypeIndex, LeptonCoord Position, byte Facing, int Health);

/// <summary>Plain-data unit type the Data layer hands to the sim. Speed in leptons per tick.</summary>
public sealed record UnitTypeDef(string Id, int Speed, int MaxHealth);

/// <summary>Everything needed to start a deterministic game.</summary>
public sealed record SimSetup(ulong Seed, int MapWidth, int MapHeight, int PlayerCount,
    IReadOnlyList<UnitTypeDef> UnitTypes, ulong RulesHash);
