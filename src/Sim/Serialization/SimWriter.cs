using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Canonical little-endian state serialisation shared by Save and the state hash.</summary>
public static class SimWriter
{
    public static readonly byte[] SaveMagic = "RTSS"u8.ToArray();
    public const int SaveVersion = 4;

    /// <summary>State body: tick, RNG, then each part's name and data in the given (canonical) order.
    /// Used verbatim by the hash.</summary>
    public static void WriteState(BinaryWriter w, int tick, SimRandom rng, IReadOnlyList<ISimStatePart> parts)
    {
        w.Write(tick);
        (ulong state, ulong inc) = rng.GetState();
        w.Write(state); w.Write(inc);
        for (int i = 0; i < parts.Count; i++)
        {
            w.Write(parts[i].Name);
            parts[i].Write(w);
        }
    }

    public static void WriteSave(Stream output, ulong rulesHash, ulong mapHash, int tick, SimRandom rng,
        IReadOnlyList<ISimStatePart> parts)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(SaveMagic);
        w.Write(SaveVersion);
        w.Write(rulesHash);
        w.Write(mapHash);
        WriteState(w, tick, rng, parts);
    }
}
