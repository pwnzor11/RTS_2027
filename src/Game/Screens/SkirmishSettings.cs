// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// The skirmish choices from the setup screen (design 08 §2, M4-11): the map, 2 to 4 slots (each Closed, Human or AI with a faction,
/// AI tier, colour and team) and the simulation seed. No Human slot means a spectator match.
/// <para>
/// Player numbering: the Closed slots are dropped and the rest become sim players 0..N-1 with the Human first (the game screen
/// treats player 0 as the local player), then the AIs in slot order. <see cref="PlayerSlots"/> lists the slot behind each player.
/// </para>
/// </summary>
/// <param name="Map">The map: a curated or user map file, or generator parameters.</param>
/// <param name="Slots">2 to 4 slots, in screen order.</param>
/// <param name="SimSeed">Seed of the simulation and of the AI driver.</param>
public sealed record SkirmishSettings(MapChoice Map, IReadOnlyList<SkirmishSlot> Slots, ulong SimSeed)
{
    /// <summary>Most slots a skirmish has.</summary>
    public const int MaxSlots = 4;

    /// <summary>Fewest slots a skirmish has (and fewest non-closed ones).</summary>
    public const int MinPlayers = 2;

    /// <summary>The curated map (file name under data/maps).</summary>
    public const string CuratedFile = SkirmishGame.CuratedMapFile;

    /// <summary>Default AI tier of the legacy two-player settings.</summary>
    public const int DefaultAiTier = 2;

    /// <summary>The old two-player settings: a human then an AI, a map seed (0 = curated Rust Belt, else a generated small map whose
    /// seed is also the simulation seed), the AI tier, the human's colour and the two factions. The AI takes colour 1 (0 if the
    /// human has 1) and the two sides are on teams 0 and 1.</summary>
    public SkirmishSettings(ulong mapSeed, int aiTier, int playerColour, Faction playerFaction = Faction.Authority,
        Faction aiFaction = Faction.Authority)
        : this(mapSeed == 0 ? new MapChoice(CuratedFile, null) : new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 2, mapSeed)),
            new[]
            {
                new SkirmishSlot(SlotKind.Human, playerFaction, aiTier, playerColour, 0),
                new SkirmishSlot(SlotKind.Ai, aiFaction, aiTier, playerColour == 1 ? 0 : 1, 1),
            },
            mapSeed == 0 ? SkirmishGame.CuratedSimSeed : mapSeed)
    {
    }

    /// <summary>The curated map, a human against a tier 2 (Regular) AI, Authority on both sides: today's default skirmish.</summary>
    public static SkirmishSettings Default { get; } = new(0, DefaultAiTier, 0);

    /// <summary>The slot indices that are not Closed, in player order (Human first, then the others in slot order).</summary>
    public IReadOnlyList<int> PlayerSlots
    {
        get
        {
            var order = new List<int>();
            for (int i = 0; i < Slots.Count; i++) if (Slots[i].Kind == SlotKind.Human) order.Add(i);
            for (int i = 0; i < Slots.Count; i++) if (Slots[i].Kind == SlotKind.Ai) order.Add(i);
            return order;
        }
    }

    /// <summary>Number of sim players: the slots that are not Closed.</summary>
    public int PlayerCount => PlayerSlots.Count;

    /// <summary>True when no slot is Human: the game is AI against AI and the player watches.</summary>
    public bool IsSpectator => !Slots.Any(s => s.Kind == SlotKind.Human);

    /// <summary>Legacy: 0 for a map file, else the generator seed.</summary>
    public ulong MapSeed => Map.Generated?.Seed ?? 0;

    /// <summary>Legacy: the AI tier of the first AI slot (the default tier when there is none).</summary>
    public int AiTier => Slots.FirstOrDefault(s => s.Kind == SlotKind.Ai)?.AiTier ?? DefaultAiTier;

    /// <summary>Legacy: the colour of the first player (Human, else slot 0).</summary>
    public int PlayerColour => PlayerSlots.Count > 0 ? Slots[PlayerSlots[0]].Colour : 0;

    /// <summary>Legacy: faction of player 0.</summary>
    public Faction PlayerFaction => PlayerSlots.Count > 0 ? Slots[PlayerSlots[0]].Faction : Faction.Authority;

    /// <summary>Legacy: faction of player 1.</summary>
    public Faction AiFaction => PlayerSlots.Count > 1 ? Slots[PlayerSlots[1]].Faction : Faction.Authority;

    /// <summary>
    /// The value for <c>SimSetup.Factions</c>, one entry per player. Null when every player is Authority, so a default skirmish
    /// builds exactly the M2 setup.
    /// </summary>
    // ASSUMED (M3-08): an all-Authority pick maps to null (the pre-M3 setup) rather than an explicit list.
    public IReadOnlyList<Faction>? Factions
    {
        get
        {
            Faction[] all = PlayerSlots.Select(i => Slots[i].Faction).ToArray();
            return all.All(f => f == Faction.Authority) ? null : all;
        }
    }

    /// <summary>The team a new slot gets by default. A 4-player game on a layout-2 map (teammates side by side, M12-07) defaults to teams 1+2
    /// against 3+4; every other case keeps the old default of one team per slot. The setup screen still lets the player change it.</summary>
    public static int DefaultTeam(int index, int players, int layout) =>
        layout == MapGenerator.ZonedLayout && players == 4 ? index / 2 : Math.Min(index, MaxSlots - 1);

    /// <summary>
    /// The value for <c>SimSetup.Teams</c>, one entry per player. Slot team numbers are renumbered by first appearance in player order,
    /// and null is returned when that gives every player its own team in order (the sim default), as the old two-player game.
    /// </summary>
    public IReadOnlyList<byte>? Teams
    {
        get
        {
            var seen = new List<int>();
            var teams = new byte[PlayerCount];
            int p = 0;
            foreach (int i in PlayerSlots)
            {
                int t = Slots[i].Team;
                int rank = seen.IndexOf(t);
                if (rank < 0) { rank = seen.Count; seen.Add(t); }
                teams[p++] = (byte)rank;
            }
            for (int k = 0; k < teams.Length; k++) if (teams[k] != k) return teams;
            return null;
        }
    }

    /// <summary>The slot kinds of the slot behind each player, for the AIs: (player, slot) pairs of the AI slots.</summary>
    public IReadOnlyList<(byte Player, SkirmishSlot Slot)> AiPlayers
    {
        get
        {
            var list = new List<(byte, SkirmishSlot)>();
            IReadOnlyList<int> order = PlayerSlots;
            for (int p = 0; p < order.Count; p++) if (Slots[order[p]].Kind == SlotKind.Ai) list.Add(((byte)p, Slots[order[p]]));
            return list;
        }
    }

    /// <summary>Why these settings cannot start a game, one message per problem; empty when they can.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Slots.Count < MinPlayers || Slots.Count > MaxSlots) errors.Add($"A skirmish has {MinPlayers} to {MaxSlots} slots.");
        int open = 0, humans = 0;
        var colours = new HashSet<int>();
        var teams = new HashSet<int>();
        for (int i = 0; i < Slots.Count; i++)
        {
            SkirmishSlot s = Slots[i];
            if (s.Kind == SlotKind.Closed) continue;
            open++;
            if (s.Kind == SlotKind.Human) humans++;
            if (s.Kind == SlotKind.Ai && (s.AiTier < 1 || s.AiTier > DifficultyLoader.TierIds.Count))
                errors.Add($"Slot {i + 1}: AI tier must be 1 to {DifficultyLoader.TierIds.Count}.");
            if (s.Colour < 0 || s.Colour >= PlayerColours.Count) errors.Add($"Slot {i + 1}: unknown colour.");
            else if (!colours.Add(s.Colour)) errors.Add($"Slot {i + 1}: colour is used by another slot.");
            if (s.Team < 0 || s.Team >= MaxSlots) errors.Add($"Slot {i + 1}: team must be 1 to {MaxSlots}.");
            teams.Add(s.Team);
            if (!Enum.IsDefined(s.Faction) || !RuleCounts.IsPlayable(s.Faction)) errors.Add($"Slot {i + 1}: unknown faction.");
        }
        if (open < MinPlayers) errors.Add("At least two slots must be open.");
        if (humans > 1) errors.Add("Only one slot can be Human.");
        if (open >= MinPlayers && teams.Count < 2) errors.Add("All players are on one team: there is nobody to fight.");
        if ((Map.File is null) == (Map.Generated is null)) errors.Add("Choose a map.");
        else if (Map.Generated is { } g)
        {
            if (g.Players < MinPlayers || g.Players > MaxSlots) errors.Add("A generated map has 2 to 4 players.");
            else if (open >= MinPlayers && g.Players != open) errors.Add($"The generated map is for {g.Players} players but {open} slots are open.");
        }
        return errors;
    }

    /// <summary>The generator parameters with the player count set to the open slots (null for a map file).</summary>
    public MapGenParams? ResolvedGenerator => Map.Generated is { } g ? g with { Players = PlayerCount } : null;

    /// <summary>Value equality that compares the slots one by one (a record would compare the list by reference).</summary>
    public bool Equals(SkirmishSettings? other) =>
        other is not null && Map == other.Map && SimSeed == other.SimSeed && Slots.SequenceEqual(other.Slots);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(Map);
        h.Add(SimSeed);
        foreach (SkirmishSlot s in Slots) h.Add(s);
        return h.ToHashCode();
    }
}
