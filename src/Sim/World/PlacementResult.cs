namespace Rts.Sim.World;

/// <summary>Outcome of <c>Simulation.CanPlace</c>. Append only.</summary>
public enum PlacementResult : byte
{
    /// <summary>The building may be placed.</summary>
    Ok = 0,
    /// <summary>Part of the footprint lies outside the map.</summary>
    OutOfMap = 1,
    /// <summary>Terrain, overlay, another building or a unit occupies a footprint cell.</summary>
    Blocked = 2,
    /// <summary>Placing it would split a region of passable cells (one-cell gap rule).</summary>
    SealsRegion = 3,
}
