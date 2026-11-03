namespace Rts.Sim.Core;

/// <summary>
/// A piece of sim state that saves, loads and hashes itself. The Simulation keeps parts in one fixed,
/// documented order; the state hash and saves iterate them in that order, and per-part sub-hashes name
/// the subsystem that diverged (M1-10). Every new sim array must live in a registered part.
/// </summary>
public interface ISimStatePart
{
    /// <summary>Stable short name, e.g. "units", "map". Persisted in saves.</summary>
    string Name { get; }
    void Write(BinaryWriter w);
    void Read(BinaryReader r);
}
