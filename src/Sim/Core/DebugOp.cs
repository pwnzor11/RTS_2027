namespace Rts.Sim.Core;

/// <summary>
/// Sub-operation of <see cref="CommandKind.Debug"/>, carried in Command.Arg (M1.5). Debug commands are recorded
/// like any other command, and the sim applies them only when <see cref="SimSetup.AllowDebug"/> is true.
/// Append only; values are persisted in replays.
/// </summary>
public enum DebugOp
{
    /// <summary>Draw one number from the sim RNG (test hook).</summary>
    BurnRng = 1,
    /// <summary>Spawn a vehicle: Arg2 = vehicle type index, Cell = cell, owner = Command.Player.</summary>
    SpawnVehicle = 2,
    /// <summary>Spawn an infantry soldier: Arg2 = infantry type index, Cell = cell, owner = Command.Player.</summary>
    SpawnInfantry = 3,
    /// <summary>Set Command.Player's cash to Arg2 (clamped to 0..int.MaxValue).</summary>
    SetCash = 4,
    /// <summary>Kill the Subject; Target.Index low byte unused, the subject's store is Arg2 = (int)ActorKind.</summary>
    Kill = 5,
}
