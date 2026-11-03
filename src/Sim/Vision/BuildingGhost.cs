using Rts.Sim.Core;

namespace Rts.Sim.Vision;

/// <summary>Last-seen image of a building that a viewer can no longer see. Health is the last-seen value.</summary>
public readonly record struct BuildingGhost(byte Player, int TypeIndex, CellCoord Cell, int FootprintW, int FootprintH,
    int Health);
