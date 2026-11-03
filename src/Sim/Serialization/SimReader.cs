using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Reads what SimWriter wrote. Throws InvalidDataException on any header mismatch.</summary>
public static class SimReader
{
    public readonly record struct State(int Tick, SimRandom Rng);

    public static State ReadSave(Stream input, ulong expectedRulesHash, ulong expectedMapHash,
        IReadOnlyList<ISimStatePart> parts)
    {
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        byte[] magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(SimWriter.SaveMagic)) throw new InvalidDataException("Bad save magic.");
        int version = r.ReadInt32();
        if (version != SimWriter.SaveVersion) throw new InvalidDataException($"Unsupported save version {version}.");
        if (r.ReadUInt64() != expectedRulesHash) throw new InvalidDataException("Rules hash mismatch.");
        if (r.ReadUInt64() != expectedMapHash) throw new InvalidDataException("Map hash mismatch.");
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
