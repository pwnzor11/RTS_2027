using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Serialization;

/// <summary>Canonical little-endian state serialisation shared by Save and the state hash.</summary>
public static class SimWriter
{
    public static readonly byte[] SaveMagic = "RTSS"u8.ToArray();
    public const int SaveVersion = 1;

    /// <summary>State body: tick, RNG, map size, unit store. Used verbatim by the hash.</summary>
    public static void WriteState(BinaryWriter w, int tick, SimRandom rng, int mapW, int mapH, UnitStore units)
    {
        w.Write(tick);
        (ulong state, ulong inc) = rng.GetState();
        w.Write(state); w.Write(inc);
        w.Write(mapW); w.Write(mapH);
        units.Write(w);
    }

    public static void WriteSave(Stream output, ulong rulesHash, int tick, SimRandom rng, int mapW, int mapH, UnitStore units)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(SaveMagic);
        w.Write(SaveVersion);
        w.Write(rulesHash);
        WriteState(w, tick, rng, mapW, mapH, units);
    }
}
