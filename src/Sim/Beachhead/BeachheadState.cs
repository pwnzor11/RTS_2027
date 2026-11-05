// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Beachhead;

/// <summary>
/// The "beachhead" state part (M7-02 skeleton, docs/design/11_m7_plan.md §2.4; filled by M7-07). Appended after "shipments". Per player,
/// a bit mask over the map cells (<see cref="Zone"/>, bit <c>cell % 64</c> of word <c>cell / 64</c>, cell = y * width + x): set where that
/// player's structures may be placed. The mask is recomputed in cell-index order when a source building appears or dies (like hardening,
/// D-076), so it is plain state and not derived on the fly. Empty by default.
/// Layout: version, player count, word count per player, then per player the number of non-zero words followed by (word index, word)
/// pairs in index order.
/// </summary>
public sealed class BeachheadState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Zone mask per player: <c>(width * height + 63) / 64</c> words each.</summary>
    public readonly ulong[][] Zone;

    /// <summary>Creates an empty part sized by the setup's player count and map.</summary>
    public BeachheadState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int words = (setup.Map.Width * setup.Map.Height + 63) / 64;
        Zone = new ulong[setup.PlayerCount][];
        for (int p = 0; p < Zone.Length; p++) Zone[p] = new ulong[words];
    }

    /// <inheritdoc />
    public string Name => "beachhead";

    /// <summary>True if <paramref name="cell"/> (y * width + x) is inside <paramref name="player"/>'s zone.</summary>
    public bool Contains(int player, int cell) => (Zone[player][cell >> 6] & (1UL << (cell & 63))) != 0;

    /// <summary>Empties one player's zone.</summary>
    public void Clear(int player) => Array.Clear(Zone[player]);

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(Zone.Length);
        w.Write(Zone.Length == 0 ? 0 : Zone[0].Length);
        foreach (ulong[] words in Zone)
        {
            int n = 0;
            foreach (ulong x in words) if (x != 0) n++;
            w.Write(n);
            for (int i = 0; i < words.Length; i++) if (words[i] != 0) { w.Write(i); w.Write(words[i]); }
        }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("beachhead version mismatch.");
        int players = r.ReadInt32(), words = r.ReadInt32();
        if (players != Zone.Length || words != (Zone.Length == 0 ? 0 : Zone[0].Length)) throw new InvalidDataException("beachhead size mismatch.");
        foreach (ulong[] row in Zone)
        {
            Array.Clear(row);
            int n = StateIo.Count(r, Name);
            for (int k = 0; k < n; k++)
            {
                int i = r.ReadInt32();
                if (i < 0 || i >= words) throw new InvalidDataException("beachhead word out of range.");
                row[i] = r.ReadUInt64();
            }
        }
    }
}
