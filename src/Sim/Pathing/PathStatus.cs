namespace Rts.Sim.Pathing;

/// <summary>Outcome of polling a path request. Append only.</summary>
public enum PathStatus
{
    /// <summary>Still queued; not solved yet.</summary>
    Pending = 0,
    /// <summary>A full path to the goal.</summary>
    Found = 1,
    /// <summary>Goal unreachable; the path leads to the reachable cell closest to the goal.</summary>
    Partial = 2,
    /// <summary>No path (and no progress possible).</summary>
    NotFound = 3,
    /// <summary>Unknown, cancelled or already released request id / index.</summary>
    Unknown = 4,
}
