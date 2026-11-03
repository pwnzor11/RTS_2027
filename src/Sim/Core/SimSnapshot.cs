using Rts.Sim.Rules;

namespace Rts.Sim.Core;

/// <summary>Read-only view of sim state for the renderer, taken after each Step. Never fed back into the sim.
/// Land is row-major (y * MapWidth + x). StateHash is the latest full hash (0 until the first one).</summary>
public sealed record SimSnapshot(int Tick, int MapWidth, int MapHeight, IReadOnlyList<UnitView> Units,
    IReadOnlyList<LandType> Land, ulong StateHash);

/// <summary>What kind of actor a UnitView is.</summary>
public enum UnitKind : byte { Vehicle = 0, Infantry = 1 }

/// <summary>One unit as the renderer sees it. Facing is 0..255 (0 = north, clockwise). TypeIndex indexes
/// the list for its Kind in SimRules.</summary>
public readonly record struct UnitView(EntityId Id, UnitKind Kind, byte Player, int TypeIndex, LeptonCoord Position,
    byte Facing, int Health);

/// <summary>Everything needed to start a deterministic game. RulesHash covers the rules; the map hash is
/// computed by the sim from Map.</summary>
public sealed record SimSetup(ulong Seed, Map.MapData Map, int PlayerCount, SimRules Rules, ulong RulesHash);
