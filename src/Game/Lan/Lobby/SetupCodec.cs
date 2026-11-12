// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Net;

namespace Rts.Game.Lan.Lobby;

/// <summary>Encodes <see cref="SetupData"/> as the setup blob of <c>NetMessage.Start</c> and decodes it (M9-05). Versioned, bounded, strict.</summary>
public static class SetupCodec
{
    /// <summary>Encodes the settings.</summary>
    public static byte[] Encode(SetupData d)
    {
        ArgumentNullException.ThrowIfNull(d);
        var w = new BlobWriter();
        w.U8(LobbyCodec.Version);
        LobbyCodec.WriteMap(w, d.Map);
        w.U64(d.SimSeed);
        w.U8(d.CommandDelay);
        w.U8(d.Slots.Count);
        foreach (SkirmishSlot s in d.Slots) LobbyCodec.WriteSlot(w, s);
        w.U8(d.PeerSlots.Count);
        foreach (KeyValuePair<int, int> p in d.PeerSlots.OrderBy(p => p.Key)) { w.I32(p.Key); w.U8(p.Value); }
        w.U8(d.Spectators.Count);
        foreach (int s in d.Spectators) w.I32(s);
        w.U8(d.Names.Count);
        foreach (KeyValuePair<int, string> n in d.Names.OrderBy(n => n.Key)) { w.I32(n.Key); w.Str(n.Value, LobbyData.MaxNameBytes); }
        return w.ToArray();
    }

    /// <summary>Decodes the settings; throws <see cref="InvalidDataException"/> on anything wrong (including peers without a name or slots out of range).</summary>
    public static SetupData Decode(byte[] blob)
    {
        var r = new BlobReader(blob);
        if (r.U8() != LobbyCodec.Version) throw new InvalidDataException("Unknown setup version.");
        LobbyMap map = LobbyCodec.ReadMap(r);
        ulong seed = r.U64();
        int delay = r.U8();
        if (!NetProtocol.DelayChoices.Contains(delay)) throw new InvalidDataException("Bad command delay.");
        int n = r.U8();
        if (n < SkirmishSettings.MinPlayers || n > SkirmishSettings.MaxSlots) throw new InvalidDataException("Bad slot count.");
        var slots = new List<SkirmishSlot>(n);
        for (int i = 0; i < n; i++) slots.Add(LobbyCodec.ReadSlot(r));

        int np = r.U8();
        if (np > SkirmishSettings.MaxSlots) throw new InvalidDataException("Too many players.");
        var peerSlots = new Dictionary<int, int>();
        var usedSlots = new HashSet<int>();
        for (int i = 0; i < np; i++)
        {
            int peer = r.I32();
            int slot = r.U8();
            if (peer < 0 || peer > 1000 || slot >= n || slots[slot].Kind != SlotKind.Human) throw new InvalidDataException("Bad player entry.");
            if (!peerSlots.TryAdd(peer, slot) || !usedSlots.Add(slot)) throw new InvalidDataException("Duplicate player entry.");
        }

        int ns = r.U8();
        if (ns > NetProtocol.MaxSpectators) throw new InvalidDataException("Too many spectators.");
        var specs = new List<int>();
        for (int i = 0; i < ns; i++)
        {
            int peer = r.I32();
            if (peer < 0 || peer > 1000 || peerSlots.ContainsKey(peer) || specs.Contains(peer)) throw new InvalidDataException("Bad spectator.");
            specs.Add(peer);
        }

        int nn = r.U8();
        if (nn > SkirmishSettings.MaxSlots + NetProtocol.MaxSpectators) throw new InvalidDataException("Too many names.");
        var names = new Dictionary<int, string>();
        for (int i = 0; i < nn; i++)
        {
            int peer = r.I32();
            if (!names.TryAdd(peer, r.Str(LobbyData.MaxNameBytes))) throw new InvalidDataException("Duplicate name.");
        }

        r.End();
        foreach (int p in peerSlots.Keys.Concat(specs)) if (!names.ContainsKey(p)) throw new InvalidDataException("A peer has no name.");
        return new SetupData(map, slots, seed, peerSlots, specs, names, delay);
    }
}
