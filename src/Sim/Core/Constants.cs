namespace Rts.Sim.Core;

/// <summary>Global simulation constants (design/01_architecture.md §3). Integer only.</summary>
public static class SimConstants
{
    public const int TicksPerSecond = 20;
    public const int LeptonsPerCell = 256;
    public const int LeptonShift = 8;          // cell = lepton >> 8
    public const int HashIntervalTicks = 16;
    public const int HashRingSize = 64;
    public const int MaxMapCells = 128;
}
