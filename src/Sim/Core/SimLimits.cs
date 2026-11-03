namespace Rts.Sim.Core;

/// <summary>
/// Store capacities for one game (M1.5, review B-4). Part of the setup: changing a limit changes the save
/// format and the hash, so replays and saves record the limits they were made with. Capacities count usable
/// slots + 1 (slot 0 is the reserved "none" slot), matching SlotArray.
/// </summary>
public sealed record SimLimits(int Vehicles, int Infantry, int Buildings, int Projectiles)
{
    /// <summary>The M1 capacities.</summary>
    public static readonly SimLimits Default = new(2048, 2048, 512, 1024);
}
