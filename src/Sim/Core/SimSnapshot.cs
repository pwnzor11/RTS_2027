using Rts.Sim.Rules;

namespace Rts.Sim.Core;

/// <summary>Read-only view of sim state for the renderer, taken after each Step. Never fed back into the sim.
/// Land is row-major (y * MapWidth + x). StateHash is the latest full hash (0 until the first one).
/// Viewer = the player whose view this is (-1 = omniscient debug view). Vision is per cell, row-major:
/// 0 = shroud (never seen), 1 = fog (seen before), 2 = visible now; empty when Viewer = -1.
/// With a viewer, enemy units outside Vision 2 are omitted and enemy buildings outside it are last-seen ghosts.</summary>
public sealed record SimSnapshot(int Tick, int MapWidth, int MapHeight, IReadOnlyList<UnitView> Units,
    IReadOnlyList<LandType> Land, ulong StateHash, IReadOnlyList<BuildingView> Buildings, int Viewer,
    IReadOnlyList<byte> Vision);

/// <summary>One building as the renderer sees it. Cell = top-left of its footprint. Ghost = last-seen
/// image of an enemy building now under fog (Health is the last-seen value).</summary>
public readonly record struct BuildingView(EntityId Id, byte Player, int TypeIndex, CellCoord Cell, int FootprintW,
    int FootprintH, int Health, bool Ghost);

/// <summary>What kind of actor a UnitView is.</summary>
public enum UnitKind : byte { Vehicle = 0, Infantry = 1 }

/// <summary>One unit as the renderer sees it. Facing is 0..255 (0 = north, clockwise). TypeIndex indexes
/// the list for its Kind in SimRules.</summary>
public readonly record struct UnitView(EntityId Id, UnitKind Kind, byte Player, int TypeIndex, LeptonCoord Position,
    byte Facing, int Health);

/// <summary>Everything needed to start a deterministic game. RulesHash covers the rules; the map hash is
/// computed by the sim from Map.</summary>
public sealed record SimSetup(ulong Seed, Map.MapData Map, int PlayerCount, SimRules Rules, ulong RulesHash);
