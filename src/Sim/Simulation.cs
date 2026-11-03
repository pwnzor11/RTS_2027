using Rts.Sim.Core;

namespace Rts.Sim;

/// <summary>
/// Public face of the simulation (design/01_architecture.md §3–5). CONTRACT STUB written by the lead;
/// package WP-SIM fills in the bodies without changing these signatures.
/// </summary>
public sealed class Simulation
{
    public Simulation(SimSetup setup) { Setup = setup; }

    public SimSetup Setup { get; }

    /// <summary>The only clock. Starts at 0; Step increments it by one.</summary>
    public int Tick { get; private set; }

    /// <summary>Setup/scripting entry: place a unit outside Step. Returns its id.</summary>
    public EntityId SpawnUnit(byte player, int unitTypeIndex, CellCoord cell) => throw new NotImplementedException();

    /// <summary>Advance exactly one tick, applying commands whose ExecTick == Tick (others are ignored).</summary>
    public void Step(IReadOnlyList<Command> commands) => throw new NotImplementedException();

    /// <summary>64-bit xxHash64 over all sim state, including RNG and Tick.</summary>
    public ulong ComputeStateHash() => throw new NotImplementedException();

    public SimSnapshot Snapshot() => throw new NotImplementedException();

    /// <summary>Versioned binary save of the whole sim state.</summary>
    public void Save(Stream output) => throw new NotImplementedException();

    /// <summary>Restore a game saved with Save. The setup must match (checked via RulesHash).</summary>
    public static Simulation Load(Stream input, SimSetup setup) => throw new NotImplementedException();
}
