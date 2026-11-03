using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Serialization;

/// <summary>Reads what SimWriter wrote. Throws InvalidDataException on any header mismatch.</summary>
public static class SimReader
{
    public readonly record struct State(int Tick, SimRandom Rng);

    public static State ReadSave(Stream input, ulong expectedRulesHash, int mapW, int mapH, UnitStore units)
    {
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        byte[] magic = r.ReadBytes(4);
        if (!magic.AsSpan().SequenceEqual(SimWriter.SaveMagic)) throw new InvalidDataException("Bad save magic.");
        int version = r.ReadInt32();
        if (version != SimWriter.SaveVersion) throw new InvalidDataException($"Unsupported save version {version}.");
        ulong rules = r.ReadUInt64();
        if (rules != expectedRulesHash) throw new InvalidDataException("Rules hash mismatch.");
        int tick = r.ReadInt32();
        ulong state = r.ReadUInt64(), inc = r.ReadUInt64();
        if (r.ReadInt32() != mapW || r.ReadInt32() != mapH) throw new InvalidDataException("Map size mismatch.");
        units.Read(r);
        return new State(tick, SimRandom.FromState(state, inc));
    }
}
