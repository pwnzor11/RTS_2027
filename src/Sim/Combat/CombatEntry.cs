using Rts.Sim.Core;

namespace Rts.Sim.Combat;

/// <summary>
/// Per-actor combat data (one per vehicle / infantry / building slot). All-default means "nothing going on".
/// ReloadTicks counts down to 0 in RunCombat; TurretFacing is valid once TurretInit (set lazily from the hull facing);
/// AttackTarget is the current victim (Id.IsNone = none); GuardPoint doubles as the AttackMove destination;
/// GuardPending marks a GuardPoint set by a command that the next Guard/AttackMove start must keep;
/// ChaseCell/RetargetTick throttle chase re-targeting; ScanTick is the next target-acquisition tick;
/// Generation (buildings) detects a recycled slot.
/// </summary>
public struct CombatEntry
{
    public int ReloadTicks;
    public byte TurretFacing;
    public bool TurretInit;
    public ActorRef AttackTarget;
    public CellCoord GuardPoint;
    public bool GuardPending;
    public CellCoord ChaseCell;
    public int RetargetTick;
    public int ScanTick;
    public ushort Generation;

    /// <summary>True when every field is at its default (such entries are written as one byte).</summary>
    public readonly bool IsDefault =>
        ReloadTicks == 0 && TurretFacing == 0 && !TurretInit && AttackTarget.Id.IsNone && AttackTarget.Kind == 0
        && GuardPoint == default && !GuardPending && ChaseCell == default && RetargetTick == 0 && ScanTick == 0
        && Generation == 0;
}
