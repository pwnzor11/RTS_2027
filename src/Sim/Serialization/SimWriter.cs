// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Canonical little-endian state serialisation shared by Save and the state hash.</summary>
public static class SimWriter
{
    public static readonly byte[] SaveMagic = "RTSS"u8.ToArray();

    /// <summary>Save format 13 (M4-17: rule-flags byte after the factions in the header; the production part appends the no-rebuild clocks when the rule is on). Format 12 (M7-02: "shipments", "beachhead" and "naval" parts after "veterancy"). Format 11 (VET-1: "veterancy" part after "salvage", shooter column in the projectiles part). Format 10 (M6-02: "rigs" and "salvage" parts after "directive", effects part version 2 with storms and withdraw flags). Format 9 (M5-01: "directive" part after "stats"). Format 8 (M4-01: "garrison" and "stats" parts, ground part version 2 with Foam). Format 7 (M3-01: factions in the header; tunnels/ground/effects parts and the players-part faction and
    /// upgrade fields in the body). Format 6 (M1.5): the <see cref="SetupHeader"/> (build, seed, players, teams, limits,
    /// AllowDebug, rules and map identity, embedded rules) precedes the state body.</summary>
    public const int SaveVersion = 16;   // S45 M-27: 16 = payload length prefix, end marker and FNV-1a 64 checksum around the save (D-220: older dev saves are refused). S44: 15 = "mines" part appended, shipments/effects/combat part versions moved (D-220: older saves are refused); 14 = "commander_orders" and "props" (A43)

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

    /// <summary>The four bytes after the payload that mark a save as complete (M-27).</summary>
    public static readonly byte[] EndMarker = "RTSE"u8.ToArray();

    /// <summary>Layout (format 16): magic, version, i32 payload length, payload (setup header + state body), end marker, u64 FNV-1a of every byte before the marker.
    /// The hash is outside the state hash: it is a file check only, and the sim state hashes do not change.</summary>
    public static void WriteSave(Stream output, SetupHeader header, int tick, SimRandom rng,
        IReadOnlyList<ISimStatePart> parts)
    {
        using var payload = new MemoryStream();
        using (var pw = new BinaryWriter(payload, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            header.Write(pw);
            WriteState(pw, tick, rng, parts);
        }

        using var head = new MemoryStream();
        using (var hw = new BinaryWriter(head, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            hw.Write(SaveMagic);
            hw.Write(SaveVersion);
            hw.Write((int)payload.Length);
        }

        ulong sum = Fnv1a(Fnv1a(FnvOffset, head.GetBuffer().AsSpan(0, (int)head.Length)), payload.GetBuffer().AsSpan(0, (int)payload.Length));
        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(head.GetBuffer(), 0, (int)head.Length);
        w.Write(payload.GetBuffer(), 0, (int)payload.Length);
        w.Write(EndMarker);
        w.Write(sum);
    }

    /// <summary>FNV-1a 64-bit offset basis.</summary>
    public const ulong FnvOffset = 14695981039346656037UL;

    /// <summary>Continues a 64-bit FNV-1a hash over <paramref name="data"/>.</summary>
    public static ulong Fnv1a(ulong h, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data) { h ^= b; h *= 1099511628211UL; }
        return h;
    }
}
