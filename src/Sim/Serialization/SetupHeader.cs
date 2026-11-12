// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Serialization;

/// <summary>
/// The identity of a game, written at the start of every replay and save (M1.5, review A-2/A-3/A-6): build string,
/// seed, player count, teams, store limits, AllowDebug, rules hash, map hash and name, the rules source files
/// (name, text) the game was started with, stored deflate-compressed, and (M3-01, format 7) the factions. Lets a save or replay be understood without
/// the caller supplying the rules, and lets a different build refuse it with a clear message.
/// </summary>
public sealed record SetupHeader(
    string Build, ulong Seed, int PlayerCount, IReadOnlyList<byte>? Teams, SimLimits Limits, bool AllowDebug,
    ulong RulesHash, ulong MapHash, int MapWidth, int MapHeight, string MapName,
    IReadOnlyList<(string Name, string Text)> RulesFiles)
{
    /// <summary>Faction per player (M3-01); null = all Authority. Written after the rules files.</summary>
    public IReadOnlyList<Rules.Faction>? Factions { get; init; }

    /// <summary>M4-17 (D-173): the skirmish defeat rule is on. Written after the factions (save and replay format 13); older replays read as false.</summary>
    public bool NoRebuildDefeat { get; init; }

    /// <summary>S19 D-205 (M13-15): handicap percent per player; null = everyone at 100. Written after the rule flags only when some player is off
    /// the defaults (flag bit 1), so other games and older replays keep their bytes.</summary>
    public IReadOnlyList<int>? Handicap { get; init; }

    /// <summary>S19 D-205 (M13-15): starting credits per player (an entry below 0 = the rules' amount); null = the rules' amount for everyone.
    /// Written together with <see cref="Handicap"/>.</summary>
    public IReadOnlyList<int>? StartingCash { get; init; }

    private const int MaxLimit = 1 << 20;
    private const int MaxStartingCash = 100_000_000;

    /// <summary>Header for a running game.</summary>
    public static SetupHeader From(Simulation sim, string build, IReadOnlyList<(string Name, string Text)> rulesFiles)
    {
        SimSetup s = sim.Setup;
        return new SetupHeader(build, s.Seed, s.PlayerCount, s.Teams, s.Limits, s.AllowDebug, s.RulesHash, sim.MapHash,
            s.Map.Width, s.Map.Height, s.Map.Name, rulesFiles)
        {
            Factions = s.Factions, NoRebuildDefeat = s.NoRebuildDefeat,
            Handicap = s.HasSlotOptions ? s.Handicap : null, StartingCash = s.HasSlotOptions ? s.StartingCash : null,
        };
    }

    /// <summary>Writes the header fields in their fixed order.</summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Build);
        w.Write(Seed);
        w.Write(PlayerCount);
        if (Teams is null) w.Write((byte)0);
        else
        {
            w.Write((byte)1);
            w.Write(Teams.Count);
            for (int i = 0; i < Teams.Count; i++) w.Write(Teams[i]);
        }
        w.Write(Limits.Vehicles); w.Write(Limits.Infantry); w.Write(Limits.Buildings); w.Write(Limits.Projectiles);
        w.Write(AllowDebug);
        w.Write(RulesHash);
        w.Write(MapHash);
        w.Write(MapWidth); w.Write(MapHeight);
        w.Write(MapName);
        RulesBlob.Write(w, RulesFiles);
        if (Factions is null) w.Write((byte)0);
        else
        {
            w.Write((byte)1);
            w.Write(Factions.Count);
            for (int i = 0; i < Factions.Count; i++) w.Write((byte)Factions[i]);
        }
        bool options = HasSlotOptions;
        w.Write((byte)((NoRebuildDefeat ? 1 : 0) | (options ? 2 : 0)));   // M4-17, format 13; bit 1 = slot options follow (S19, M13-15)
        if (!options) return;
        for (int p = 0; p < PlayerCount; p++)
        {
            w.Write((byte)HandicapOf(p));
            w.Write(StartingCashOf(p));
        }
    }

    private int HandicapOf(int p) => Handicap != null && p < Handicap.Count ? Handicap[p] : SimSetup.DefaultHandicap;
    private int StartingCashOf(int p) => StartingCash != null && p < StartingCash.Count ? StartingCash[p] : -1;

    private bool HasSlotOptions
    {
        get
        {
            for (int p = 0; p < PlayerCount; p++) if (HandicapOf(p) != SimSetup.DefaultHandicap || StartingCashOf(p) >= 0) return true;
            return false;
        }
    }

    /// <summary>Reads what <see cref="Write"/> wrote. Throws InvalidDataException on a malformed or truncated header.</summary>
    public static SetupHeader Read(BinaryReader r, bool hasRuleFlags = true)
    {
        try
        {
            string build = r.ReadString();
            ulong seed = r.ReadUInt64();
            int players = r.ReadInt32();
            if (players < 1 || players > byte.MaxValue) throw new InvalidDataException($"Bad player count {players}.");
            byte[]? teams = null;
            byte hasTeams = r.ReadByte();
            if (hasTeams == 1)
            {
                int n = r.ReadInt32();
                if (n != players) throw new InvalidDataException("Teams count does not match the player count.");
                teams = r.ReadBytes(n);
                if (teams.Length != n) throw new InvalidDataException("Truncated header.");
            }
            else if (hasTeams != 0) throw new InvalidDataException($"Bad teams flag {hasTeams}.");
            var limits = new SimLimits(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            if (limits.Vehicles < 2 || limits.Infantry < 2 || limits.Buildings < 2 || limits.Projectiles < 2
                || limits.Vehicles > MaxLimit || limits.Infantry > MaxLimit || limits.Buildings > MaxLimit || limits.Projectiles > MaxLimit)
                throw new InvalidDataException("Bad store limits.");
            bool allowDebug = r.ReadBoolean();
            ulong rulesHash = r.ReadUInt64(), mapHash = r.ReadUInt64();
            int w = r.ReadInt32(), h = r.ReadInt32();
            string mapName = r.ReadString();
            var files = RulesBlob.Read(r);
            Rules.Faction[]? factions = null;
            byte hasFactions = r.ReadByte();
            if (hasFactions == 1)
            {
                int n = r.ReadInt32();
                if (n != players) throw new InvalidDataException("Factions count does not match the player count.");
                factions = new Rules.Faction[n];
                for (int i = 0; i < n; i++)
                {
                    byte f = r.ReadByte();
                    // M7-02: Expedition is a valid owner before the setup offers it
                    if (f >= Rules.RuleCounts.Factions || (Rules.Faction)f == Rules.Faction.Neutral) throw new InvalidDataException($"Bad faction {f}.");
                    factions[i] = (Rules.Faction)f;
                }
            }
            else if (hasFactions != 0) throw new InvalidDataException($"Bad factions flag {hasFactions}.");
            bool noRebuild = false;
            int[]? handicap = null, startingCash = null;
            if (hasRuleFlags)   // M4-17: replays older than format 13 have no flags byte
            {
                byte flags = r.ReadByte();
                if (flags > 3) throw new InvalidDataException($"Bad rule flags {flags}.");
                noRebuild = (flags & 1) != 0;
                if ((flags & 2) != 0)   // S19 (M13-15): per-player handicap and starting credits
                {
                    handicap = new int[players];
                    startingCash = new int[players];
                    for (int p = 0; p < players; p++)
                    {
                        handicap[p] = r.ReadByte();
                        startingCash[p] = r.ReadInt32();
                        if (handicap[p] < SimSetup.MinHandicap || handicap[p] > SimSetup.MaxHandicap) throw new InvalidDataException($"Bad handicap {handicap[p]}.");
                        if (startingCash[p] < -1 || startingCash[p] > MaxStartingCash) throw new InvalidDataException($"Bad starting credits {startingCash[p]}.");
                    }
                }
            }
            return new SetupHeader(build, seed, players, teams, limits, allowDebug, rulesHash, mapHash, w, h, mapName, files)
            {
                Factions = factions,
                NoRebuildDefeat = noRebuild,
                Handicap = handicap,
                StartingCash = startingCash,
            };
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Truncated header.");
        }
    }

    /// <summary>
    /// Throws InvalidDataException naming the first way <paramref name="setup"/> (with map hash
    /// <paramref name="mapHash"/>) differs from this header: rules hash, map hash, seed (when
    /// <paramref name="checkSeed"/>), map size, player count, teams, limits or AllowDebug.
    /// </summary>
    public void CheckMatches(SimSetup setup, ulong mapHash, string what, bool checkSeed)
    {
        if (RulesHash != setup.RulesHash)
            throw new InvalidDataException($"Rules hash mismatch: {what} has 0x{RulesHash:x16}, setup has 0x{setup.RulesHash:x16}.");
        if (MapHash != mapHash)
            throw new InvalidDataException($"Map hash mismatch: {what} has 0x{MapHash:x16}, setup map is 0x{mapHash:x16}.");
        if (checkSeed && Seed != setup.Seed)
            throw new InvalidDataException($"{what} seed {Seed} does not match setup seed {setup.Seed}.");
        if (MapWidth != setup.Map.Width || MapHeight != setup.Map.Height)
            throw new InvalidDataException($"{what} map size {MapWidth}x{MapHeight} does not match setup {setup.Map.Width}x{setup.Map.Height}.");
        if (PlayerCount != setup.PlayerCount)
            throw new InvalidDataException($"{what} player count {PlayerCount} does not match setup {setup.PlayerCount}.");
        if (!TeamsEqual(Teams, setup.Teams))
            throw new InvalidDataException($"{what} teams do not match the setup.");
        if (Limits != setup.Limits)
            throw new InvalidDataException($"{what} store limits {Limits} do not match setup {setup.Limits}.");
        if (AllowDebug != setup.AllowDebug)
            throw new InvalidDataException($"{what} AllowDebug={AllowDebug} does not match setup AllowDebug={setup.AllowDebug}.");
        if (NoRebuildDefeat != setup.NoRebuildDefeat)
            throw new InvalidDataException($"{what} NoRebuildDefeat={NoRebuildDefeat} does not match the setup NoRebuildDefeat={setup.NoRebuildDefeat}.");
        for (int p = 0; p < PlayerCount; p++)
        {
            if (HandicapOf(p) != setup.HandicapOf((byte)p))
                throw new InvalidDataException($"{what} handicap of player {p} ({HandicapOf(p)}) does not match the setup ({setup.HandicapOf((byte)p)}).");
            int cash = StartingCashOf(p) >= 0 ? StartingCashOf(p) : setup.Rules.Economy.StartingCash;
            if (cash != setup.StartingCashOf((byte)p))
                throw new InvalidDataException($"{what} starting credits of player {p} ({cash}) do not match the setup ({setup.StartingCashOf((byte)p)}).");
        }
        for (int p = 0; p < PlayerCount; p++)
            if (FactionOf(p) != setup.FactionOf((byte)p))
                throw new InvalidDataException($"{what} faction of player {p} ({FactionOf(p)}) does not match the setup ({setup.FactionOf((byte)p)}).");
    }

    /// <summary>
    /// A setup for this header from rules and a map the caller loaded (for example from <see cref="RulesFiles"/>):
    /// seed, player count, teams, limits and AllowDebug come from the header. Throws InvalidDataException when the
    /// rules hash or map hash differ.
    /// </summary>
    public SimSetup ToSetup(Map.MapData map, Rules.SimRules rules, ulong rulesHash)
    {
        if (rulesHash != RulesHash)
            throw new InvalidDataException($"Rules hash 0x{rulesHash:x16} does not match the recorded 0x{RulesHash:x16}.");
        if (Map.MapHasher.Compute(map) != MapHash)
            throw new InvalidDataException($"Map hash 0x{Map.MapHasher.Compute(map):x16} does not match the recorded 0x{MapHash:x16}.");
        return new SimSetup(Seed, map, PlayerCount, rules, rulesHash)
        {
            Teams = Teams, Limits = Limits, AllowDebug = AllowDebug, Factions = Factions, NoRebuildDefeat = NoRebuildDefeat,
            Handicap = Handicap, StartingCash = StartingCash,
        };
    }

    /// <summary>Faction of a player slot (Authority when <see cref="Factions"/> is null).</summary>
    public Rules.Faction FactionOf(int player) =>
        Factions != null && player < Factions.Count ? Factions[player] : Rules.Faction.Authority;

    private static bool TeamsEqual(IReadOnlyList<byte>? a, IReadOnlyList<byte>? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
