// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Rts.Sim.Core;

namespace Rts.AI;

/// <summary>
/// The byte format around an AI's saved memory (M3-21): a small envelope (magic, format version, length, FNV-1a checksum) so a damaged or
/// foreign blob is refused and the AI simply starts fresh, plus helpers the managers use to write cells, ids and sets in a fixed order
/// (sets and maps are written sorted by key, so equal state always gives equal bytes). Not simulation state: nothing here is hashed.
/// </summary>
/// <remarks>
/// What is saved (Recruit AI, ids "recruit" / "tiered"): the seeded generator, the tier record (max tier and the ticks tier 2 / 3 were
/// reached), the build order including the structures the tier planner appended and its counters, the tier planner's memory of the enemy
/// (armour mix, defences, harvester sightings, flags), the production bags and rally cell, the wave manager (phase, target player,
/// roster, staging cells, thresholds, route position, avoid list, stuck watchdog), the scout, the squads (home, scout, raiders), raid,
/// upgrade, rescue, garrison, repair, scatter and retreat memory, and the Directorate tactics. NOT saved: the plain "Skirmisher"
/// AI (stateless apart from its generator; it restarts fresh), per-decision scratch (the knowledge lists, placement search blocks,
/// telegraph copies), caches that are re-derived from the rules on the first decision, and statistics that no decision reads.
/// </remarks>
public static class AiSnapshot
{
    /// <summary>The envelope version this build writes. A blob of another version is ignored (the AI starts fresh).</summary>
    public const int FormatVersion = 1;

    private static readonly byte[] Magic = "AISN"u8.ToArray();

    /// <summary>Wraps <paramref name="payload"/> in the envelope.</summary>
    public static byte[] Pack(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        using var ms = new MemoryStream(payload.Length + 16);
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Magic);
            w.Write((ushort)FormatVersion);
            w.Write(payload.Length);
            w.Write(Checksum(payload));
            w.Write(payload);
        }
        return ms.ToArray();
    }

    /// <summary>Opens an envelope. False (and an empty payload) for a blob that is not one, is of another version or fails its checksum.</summary>
    public static bool TryUnpack(byte[]? blob, out byte[] payload)
    {
        payload = Array.Empty<byte>();
        if (blob is null || blob.Length < 14) return false;
        try
        {
            using var r = new BinaryReader(new MemoryStream(blob), Encoding.UTF8);
            if (!r.ReadBytes(4).AsSpan().SequenceEqual(Magic)) return false;
            if (r.ReadUInt16() != FormatVersion) return false;
            int length = r.ReadInt32();
            uint sum = r.ReadUInt32();
            if (length < 0 || length != blob.Length - 14) return false;
            byte[] body = r.ReadBytes(length);
            if (Checksum(body) != sum) return false;
            payload = body;
            return true;
        }
        catch (Exception e) when (e is EndOfStreamException or IOException or ArgumentException)
        {
            return false;
        }
    }

    private static uint Checksum(byte[] data)
    {
        uint h = 2166136261;
        foreach (byte b in data) h = (h ^ b) * 16777619;
        return h;
    }

    // ---- helpers for the manager Snapshot / Restore methods ----------------------------------------------------------------

    internal static void WriteCell(BinaryWriter w, CellCoord c) { w.Write(c.X); w.Write(c.Y); }

    internal static CellCoord ReadCell(BinaryReader r) => new(r.ReadInt32(), r.ReadInt32());

    internal static void WriteId(BinaryWriter w, EntityId id) { w.Write(id.Index); w.Write(id.Generation); }

    internal static EntityId ReadId(BinaryReader r) => new(r.ReadUInt16(), r.ReadUInt16());

    /// <summary>Writes a key set sorted ascending.</summary>
    internal static void WriteKeys(BinaryWriter w, HashSet<ulong> set)
    {
        w.Write(set.Count);
        foreach (ulong k in set.OrderBy(k => k)) w.Write(k);
    }

    /// <summary>Replaces the contents of <paramref name="set"/> with a set written by <see cref="WriteKeys"/>.</summary>
    internal static void ReadKeys(BinaryReader r, HashSet<ulong> set)
    {
        set.Clear();
        int n = Count(r);
        for (int i = 0; i < n; i++) set.Add(r.ReadUInt64());
    }

    /// <summary>Writes a map sorted by key; <paramref name="value"/> writes one value.</summary>
    internal static void WriteMap<T>(BinaryWriter w, Dictionary<ulong, T> map, Action<BinaryWriter, T> value)
    {
        w.Write(map.Count);
        foreach (ulong k in map.Keys.OrderBy(k => k)) { w.Write(k); value(w, map[k]); }
    }

    /// <summary>Replaces the contents of <paramref name="map"/> with a map written by <see cref="WriteMap"/>.</summary>
    internal static void ReadMap<T>(BinaryReader r, Dictionary<ulong, T> map, Func<BinaryReader, T> value)
    {
        map.Clear();
        int n = Count(r);
        for (int i = 0; i < n; i++) { ulong k = r.ReadUInt64(); map[k] = value(r); }
    }

    /// <summary>Reads a list length, refusing absurd values from a damaged blob.</summary>
    internal static int Count(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n < 0 || n > 1 << 20) throw new InvalidDataException("bad AI snapshot count");
        return n;
    }
}
