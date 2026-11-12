// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Screens;
using Rts.Game.Sprites;
using Rts.Net;
using Rts.Sim.Rules;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// Encodes <see cref="LobbyData"/> as the blob of <c>NetMessage.LobbyState</c> and decodes it again (M9-05). Versioned (first byte), bounded
/// (4 slots, 2 spectators, short names) and strict: anything wrong, including trailing bytes, is an <see cref="InvalidDataException"/>.
/// </summary>
public static class LobbyCodec
{
    /// <summary>Blob format version.</summary>
    public const byte Version = 1;

    /// <summary>Longest map file name, in UTF-8 bytes.</summary>
    public const int MaxFileBytes = 120;

    internal static void WriteMap(BlobWriter w, LobbyMap map)
    {
        if (map.Generated is { } g)
        {
            w.U8(0);
            w.U8((int)g.Biome); w.U8((int)g.Size); w.U8(g.Players); w.U64(g.Seed);
            w.Bool(g.Name is not null); if (g.Name is not null) w.Str(g.Name, MaxFileBytes);
            w.U8((int)g.OceanEdges); w.I32(g.Layout);
        }
        else
        {
            w.U8(map.FileHash == 0 ? 1 : 2);
            w.Str(map.File ?? "", MaxFileBytes);
            if (map.FileHash != 0) w.U64(map.FileHash);
        }
    }

    internal static LobbyMap ReadMap(BlobReader r)
    {
        int kind = r.Ranged(2, "map kind");
        if (kind == 0)
        {
            int biome = r.Ranged(Enum.GetValues<Biome>().Length - 1, "biome");
            int size = r.Ranged(Enum.GetValues<MapSize>().Length - 1, "map size");
            int players = r.Ranged(SkirmishSettings.MaxSlots, "player count");
            ulong seed = r.U64();
            string? name = r.Bool() ? r.Str(MaxFileBytes) : null;
            int ocean = r.Ranged(15, "ocean edges");
            int layout = r.I32();
            if (layout < 0 || layout > 100) throw new InvalidDataException("Bad layout.");
            return LobbyMap.FromGenerated(new MapGenParams((Biome)biome, (MapSize)size, players, seed, name, (OceanEdges)ocean, layout));
        }

        string file = r.Str(MaxFileBytes);
        if (file.Length == 0 || file.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || file.Contains("..")) throw new InvalidDataException("Bad map file name.");
        ulong hash = kind == 2 ? r.U64() : 0;
        if (kind == 2 && hash == 0) throw new InvalidDataException("Bad map hash.");
        return new LobbyMap(file, hash, null);
    }

    internal static void WriteSlot(BlobWriter w, SkirmishSlot s)
    {
        w.U8((int)s.Kind); w.U8((int)s.Faction); w.U8(s.AiTier); w.U8(s.Colour); w.U8(s.Team);
        w.Bool(s.Commander); w.U8(s.Handicap); w.U8(s.CreditsPreset); w.Bool(s.SmallArmy);
    }

    internal static SkirmishSlot ReadSlot(BlobReader r)
    {
        int kind = r.Ranged((int)SlotKind.Ai, "slot kind");
        int faction = r.Ranged((int)Faction.Expedition, "faction");
        int tier = r.Ranged(DifficultyLoader.TierIds.Count, "AI tier");
        int colour = r.Ranged(PlayerColours.Count - 1, "colour");
        int team = r.Ranged(SkirmishSettings.MaxSlots - 1, "team");
        bool commander = r.Bool();
        int handicap = r.Ranged(100, "handicap");
        int credits = r.Ranged(SlotOptions.CreditsPresets.Count - 1, "credits");
        bool army = r.Bool();
        return new SkirmishSlot((SlotKind)kind, (Faction)faction, tier, colour, team, commander, handicap, credits, army);
    }

    /// <summary>Encodes the lobby.</summary>
    public static byte[] Encode(LobbyData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var w = new BlobWriter();
        w.U8(Version);
        w.Str(data.HostName, LobbyData.MaxNameBytes);
        WriteMap(w, data.Map);
        w.U8(data.CommandDelay);
        w.U8(data.Slots.Count);
        foreach (LobbySlotState s in data.Slots)
        {
            WriteSlot(w, s.Slot);
            w.I32(s.Peer); w.Str(s.Name, LobbyData.MaxNameBytes); w.Bool(s.Ready);
        }

        w.U8(data.Spectators.Count);
        foreach (LobbySpectator sp in data.Spectators) { w.I32(sp.Peer); w.Str(sp.Name, LobbyData.MaxNameBytes); }
        return w.ToArray();
    }

    /// <summary>Decodes a lobby blob; throws <see cref="InvalidDataException"/> on anything wrong.</summary>
    public static LobbyData Decode(byte[] blob)
    {
        var r = new BlobReader(blob);
        if (r.U8() != Version) throw new InvalidDataException("Unknown lobby version.");
        string host = r.Str(LobbyData.MaxNameBytes);
        LobbyMap map = ReadMap(r);
        int delay = r.U8();
        if (!NetProtocol.DelayChoices.Contains(delay)) throw new InvalidDataException("Bad command delay.");
        int n = r.U8();
        if (n < SkirmishSettings.MinPlayers || n > SkirmishSettings.MaxSlots) throw new InvalidDataException("Bad slot count.");
        var slots = new List<LobbySlotState>(n);
        for (int i = 0; i < n; i++)
        {
            SkirmishSlot slot = ReadSlot(r);
            int peer = r.I32();
            string name = r.Str(LobbyData.MaxNameBytes);
            bool ready = r.Bool();
            if (peer < LobbySlotState.NoPeer || peer > 1000) throw new InvalidDataException("Bad peer id.");
            if (slot.Kind != SlotKind.Human && (peer != LobbySlotState.NoPeer || name.Length > 0 || ready)) throw new InvalidDataException("Only a Human slot has a player.");
            slots.Add(new LobbySlotState(slot, peer, name, ready));
        }

        int specs = r.U8();
        if (specs > NetProtocol.MaxSpectators) throw new InvalidDataException("Too many spectators.");
        var spectators = new List<LobbySpectator>(specs);
        for (int i = 0; i < specs; i++)
        {
            int peer = r.I32();
            if (peer < 0 || peer > 1000) throw new InvalidDataException("Bad peer id.");
            spectators.Add(new LobbySpectator(peer, r.Str(LobbyData.MaxNameBytes)));
        }

        r.End();
        return new LobbyData(host, map, slots, spectators, delay);
    }
}
