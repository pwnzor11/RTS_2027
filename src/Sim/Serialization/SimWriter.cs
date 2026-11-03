using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Canonical little-endian state serialisation shared by Save and the state hash.</summary>
public static class SimWriter
{
    public static readonly byte[] SaveMagic = "RTSS"u8.ToArray();

    /// <summary>Save format 6 (M1.5): the <see cref="SetupHeader"/> (build, seed, players, teams, limits, AllowDebug,
    /// rules and map identity, embedded rules) precedes the state body.</summary>
    public const int SaveVersion = 6;

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

    public static void WriteSave(Stream output, SetupHeader header, int tick, SimRandom rng,
        IReadOnlyList<ISimStatePart> parts)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(SaveMagic);
        w.Write(SaveVersion);
        header.Write(w);
        WriteState(w, tick, rng, parts);
    }
}
