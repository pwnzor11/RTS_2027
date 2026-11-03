using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Reads what SimWriter wrote. Throws InvalidDataException on any header mismatch.</summary>
public static class SimReader
{
    public readonly record struct State(int Tick, SimRandom Rng);

    /// <summary>Reads and checks the magic and version, then the setup header. Leaves the reader at the state body.</summary>
    public static SetupHeader ReadHeader(BinaryReader r)
    {
        byte[] magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(SimWriter.SaveMagic)) throw new InvalidDataException("Bad save magic.");
        int version = r.ReadInt32();
        if (version != SimWriter.SaveVersion)
            throw new InvalidDataException($"Unsupported save version {version}; this build reads version {SimWriter.SaveVersion}. Saves from other versions cannot be loaded.");
        return SetupHeader.Read(r);
    }

    /// <summary>Reads the state body (tick, RNG, then each part) into <paramref name="parts"/>.</summary>
    public static State ReadState(BinaryReader r, IReadOnlyList<ISimStatePart> parts)
    {
        int tick = r.ReadInt32();
        ulong state = r.ReadUInt64(), inc = r.ReadUInt64();
        for (int i = 0; i < parts.Count; i++)
        {
            string name = r.ReadString();
            if (name != parts[i].Name)
                throw new InvalidDataException($"Expected state part '{parts[i].Name}', found '{name}'.");
            parts[i].Read(r);
        }
        return new State(tick, SimRandom.FromState(state, inc));
    }
}
