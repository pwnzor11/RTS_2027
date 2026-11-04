// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>Canonical little-endian state serialisation shared by Save and the state hash.</summary>
public static class SimWriter
{
    public static readonly byte[] SaveMagic = "RTSS"u8.ToArray();

    /// <summary>Save format 9 (M5-01: "directive" part after "stats"). Format 8 (M4-01: "garrison" and "stats" parts, ground part version 2 with Foam). Format 7 (M3-01: factions in the header; tunnels/ground/effects parts and the players-part faction and
    /// upgrade fields in the body). Format 6 (M1.5): the <see cref="SetupHeader"/> (build, seed, players, teams, limits,
    /// AllowDebug, rules and map identity, embedded rules) precedes the state body.</summary>
    public const int SaveVersion = 9;

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
