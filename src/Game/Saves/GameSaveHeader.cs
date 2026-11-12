// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Saves;

/// <summary>
/// The small game header of a save file (version <see cref="CurrentVersion"/>): everything needed to rebuild the screen around the
/// loaded simulation that the sim bytes do not hold. It is written before the simulation's own save bytes (see <see cref="GameSaveFile"/>).
/// </summary>
public sealed record GameSaveHeader
{
    /// <summary>The header version this build writes and reads. Bump it when the layout changes. Version 1 (two-player skirmish settings) is still read; M4-11 wrote version 2 (map choice, 2 to 4 slots); M3-21 wrote version 3 (appends each AI's saved memory after its slot record; versions 1 and 2 load with the AI starting fresh); M5-09 wrote version 4 (appends one commander byte, 0 or 1, per skirmish slot at the end; versions 1 to 3 load with no commander); M7-14 wrote version 5 (appends one ocean-edge flags byte after the commander bytes when the skirmish map is generated; versions 1 to 4 load with no ocean); M12-05 wrote version 6 (appends one generator-layout byte after the ocean-edge byte when the skirmish map is generated; versions 1 to 5 load as layout 0, the pre-M12 generator, which this build cannot rebuild, so <see cref="GameLoader"/> refuses them with a friendly message); D-205 wrote version 7 (appends the nine control groups at the very end; versions 1 to 6 load with no groups); M13-15 wrote version 8 (appends three bytes per skirmish slot after the groups: handicap percent, starting-credits preset, small-army flag; versions 1 to 7 load with every slot at the defaults).</summary>
    public const int CurrentVersion = 8;

    private const int MaxAiStateBytes = 1 << 22;

    /// <summary>Header version of this save.</summary>
    public int Version { get; init; } = CurrentVersion;

    /// <summary>Skirmish or mission.</summary>
    public SaveMode Mode { get; init; }

    /// <summary>Which map: <c>file:name.map.toml</c> (under data/maps) or <c>generated:SIZE:SEED</c>.</summary>
    public string MapId { get; init; } = "";

    /// <summary>Display name of the map.</summary>
    public string MapName { get; init; } = "";

    /// <summary>Hash of the rules the game was running on; a different hash at load time refuses the save.</summary>
    public ulong RulesHash { get; init; }

    /// <summary>The human player's slot.</summary>
    public byte LocalPlayer { get; init; }

    /// <summary>Simulation tick at save time (for the list).</summary>
    public int Tick { get; init; }

    /// <summary>Mission folder name (Mission mode), else empty.</summary>
    public string MissionId { get; init; } = "";

    /// <summary>Hash of the mission script source (Mission mode), else 0.</summary>
    public ulong ScriptHash { get; init; }

    /// <summary>The skirmish setup choices (Skirmish mode), else null.</summary>
    public SkirmishSettings? Skirmish { get; init; }

    /// <summary>The AI slots to re-attach.</summary>
    public IReadOnlyList<SavedAi> Ai { get; init; } = Array.Empty<SavedAi>();

    /// <summary>Commands queued but not yet run at save time, so a load continues exactly (the sim does not hold them).</summary>
    public IReadOnlyList<Command> Pending { get; init; } = Array.Empty<Command>();

    /// <summary>Next command sequence number of each player that has issued commands (index = player).</summary>
    public IReadOnlyList<ushort> Sequences { get; init; } = Array.Empty<ushort>();

    /// <summary>The nine control groups (index 0 = group 1) as the player had set them (D-205). Empty for a save older than header version 7 and for the sandbox.</summary>
    public IReadOnlyList<IReadOnlyList<UnitKey>> ControlGroups { get; init; } = Array.Empty<IReadOnlyList<UnitKey>>();

    /// <summary>Build version that wrote the save.</summary>
    public string Build { get; init; } = "";

    /// <summary>One line for the load list.</summary>
    public string Describe()
    {
        double minutes = Tick / (double)SimConstants.TicksPerSecond / 60.0;
        string time = FormattableString.Invariant($"{minutes:0.0} min");
        if (Mode == SaveMode.Mission) return $"Mission: {Rts.Game.Ui.DisplayNames.Mission(MissionId)}, {time}";
        string ai = Ai.Count > 0 ? $", vs {Ai[0].AiId} tier {Ai[0].Tier}" : "";
        return $"Skirmish on {Rts.Game.Ui.DisplayNames.Map(MapName)}{ai}, {time}";
    }

    /// <summary>Writes the header body (without the file envelope).</summary>
    public void Write(BinaryWriter w)
    {
        w.Write((byte)Mode);
        w.Write(MapId);
        w.Write(MapName);
        w.Write(RulesHash);
        w.Write(LocalPlayer);
        w.Write(Tick);
        w.Write(MissionId);
        w.Write(ScriptHash);
        w.Write(Build);
        w.Write(Skirmish is not null);
        if (Skirmish is { } s)
        {
            WriteSettings(w, s);
        }
        w.Write((byte)Ai.Count);
        foreach (SavedAi a in Ai)
        {
            w.Write(a.Slot);
            w.Write(a.AiId);
            w.Write(a.Tier);
            w.Write((byte)a.Faction);
            w.Write(a.Seed);
            if (Version >= 3)
            {
                w.Write(a.State is not null);
                if (a.State is not null) { w.Write(a.State.Length); w.Write(a.State); }
            }
        }
        w.Write(Pending.Count);
        foreach (Command c in Pending)
        {
            w.Write(c.ExecTick);
            w.Write(c.Player);
            w.Write(c.Sequence);
            w.Write((byte)c.Kind);
            w.Write(c.Subject.Index);
            w.Write(c.Subject.Generation);
            w.Write(c.Target.Index);
            w.Write(c.Target.Generation);
            w.Write(c.Cell.X);
            w.Write(c.Cell.Y);
            w.Write(c.Arg);
            w.Write(c.Arg2);
        }
        w.Write((ushort)Sequences.Count);
        foreach (ushort q in Sequences) w.Write(q);
        if (Version >= 4 && Skirmish is { } sk)
            foreach (SkirmishSlot slot in sk.Slots) w.Write((byte)(slot.Commander ? 1 : 0));
        if (Version >= 5 && Skirmish?.Map.Generated is { } gen) w.Write((byte)gen.OceanEdges);   // M7-14
        if (Version >= 6 && Skirmish?.Map.Generated is { } gen6) w.Write((byte)gen6.Layout);   // M12-05
        if (Version >= 7) WriteGroups(w);   // D-205
        if (Version >= 8 && Skirmish is { } opts)   // M13-15: the slot options (handicap, credits preset, small army)
            foreach (SkirmishSlot slot in opts.Slots)
            {
                w.Write((byte)slot.Handicap);
                w.Write((byte)slot.CreditsPreset);
                w.Write(slot.SmallArmy);
            }
    }

    private void WriteGroups(BinaryWriter w)
    {
        w.Write((byte)ControlGroups.Count);
        foreach (IReadOnlyList<UnitKey> group in ControlGroups)
        {
            w.Write((ushort)group.Count);
            foreach (UnitKey key in group)
            {
                w.Write((byte)key.Kind);
                w.Write(key.Id.Index);
                w.Write(key.Id.Generation);
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<UnitKey>> ReadGroups(BinaryReader r)
    {
        int count = r.ReadByte();
        if (count > Rts.Game.Controls.ControlGroups.Count) throw new InvalidDataException($"bad control group count {count}");
        var groups = new List<IReadOnlyList<UnitKey>>(count);
        for (int g = 0; g < count; g++)
        {
            int members = r.ReadUInt16();
            var list = new List<UnitKey>(members);
            for (int i = 0; i < members; i++)
            {
                var kind = (ActorKind)r.ReadByte();
                if (kind is not (ActorKind.Vehicle or ActorKind.Infantry)) throw new InvalidDataException($"bad control group unit kind {(int)kind}");
                list.Add(new UnitKey(kind, new EntityId(r.ReadUInt16(), r.ReadUInt16())));
            }
            groups.Add(list);
        }
        return groups;
    }

    /// <summary>Reads a header body written by <see cref="Write"/> for header <paramref name="version"/>.</summary>
    public static GameSaveHeader Read(BinaryReader r, int version)
    {
        var mode = (SaveMode)r.ReadByte();
        if (mode is not (SaveMode.Skirmish or SaveMode.Mission)) throw new InvalidDataException($"unknown game mode {(int)mode}");
        string mapId = r.ReadString();
        string mapName = r.ReadString();
        ulong rulesHash = r.ReadUInt64();
        byte local = r.ReadByte();
        int tick = r.ReadInt32();
        string missionId = r.ReadString();
        ulong scriptHash = r.ReadUInt64();
        string build = r.ReadString();
        SkirmishSettings? skirmish = null;
        if (r.ReadBoolean())
        {
            skirmish = version >= 2 ? ReadSettings(r) : ReadSettingsV1(r);
        }
        int aiCount = r.ReadByte();
        var ai = new List<SavedAi>(aiCount);
        for (int i = 0; i < aiCount; i++)
        {
            var slot = new SavedAi(r.ReadByte(), r.ReadString(), r.ReadInt32(), (Faction)r.ReadByte(), r.ReadUInt64());
            if (version >= 3 && r.ReadBoolean())
            {
                int length = r.ReadInt32();
                if (length < 0 || length > MaxAiStateBytes) throw new InvalidDataException("bad AI state length");
                byte[] state = r.ReadBytes(length);
                if (state.Length != length) throw new EndOfStreamException("AI state ends early");
                slot = slot with { State = state };
            }
            ai.Add(slot);
        }
        int pendingCount = r.ReadInt32();
        if (pendingCount < 0 || pendingCount > 1 << 20) throw new InvalidDataException("bad pending command count");
        var pending = new List<Command>(pendingCount);
        for (int i = 0; i < pendingCount; i++)
        {
            int exec = r.ReadInt32();
            byte player = r.ReadByte();
            ushort seq = r.ReadUInt16();
            var kind = (CommandKind)r.ReadByte();
            var subject = new EntityId(r.ReadUInt16(), r.ReadUInt16());
            var target = new EntityId(r.ReadUInt16(), r.ReadUInt16());
            var cell = new CellCoord(r.ReadInt32(), r.ReadInt32());
            pending.Add(new Command(exec, player, seq, kind, subject, target, cell, r.ReadInt32(), r.ReadInt32()));
        }
        int seqCount = r.ReadUInt16();
        var sequences = new ushort[seqCount];
        for (int i = 0; i < seqCount; i++) sequences[i] = r.ReadUInt16();
        if (version >= 4 && skirmish is not null)
        {
            var withCommanders = new List<SkirmishSlot>(skirmish.Slots.Count);
            foreach (SkirmishSlot slot in skirmish.Slots)
            {
                byte flag = r.ReadByte();
                if (flag > 1) throw new InvalidDataException($"bad commander flag {flag}");
                withCommanders.Add(slot with { Commander = flag == 1 });
            }
            skirmish = new SkirmishSettings(skirmish.Map, withCommanders, skirmish.SimSeed);
        }
        if (version >= 5 && skirmish?.Map.Generated is { } generated)   // M7-14: a missing field (older save) leaves OceanEdges.None
        {
            byte edges = r.ReadByte();
            if (edges > 15) throw new InvalidDataException($"bad ocean edge flags {edges}");
            skirmish = new SkirmishSettings(new MapChoice(null, generated with { OceanEdges = (OceanEdges)edges }), skirmish.Slots, skirmish.SimSeed);
        }
        if (version >= 6 && skirmish?.Map.Generated is { } layered)   // M12-05: the generator version; older saves keep 0
        {
            byte layout = r.ReadByte();
            skirmish = new SkirmishSettings(new MapChoice(null, layered with { Layout = layout }), skirmish.Slots, skirmish.SimSeed);
        }
        IReadOnlyList<IReadOnlyList<UnitKey>> controlGroups = version >= 7 ? ReadGroups(r) : Array.Empty<IReadOnlyList<UnitKey>>();   // D-205
        if (version >= 8 && skirmish is not null)   // M13-15
        {
            var withOptions = new List<SkirmishSlot>(skirmish.Slots.Count);
            foreach (SkirmishSlot slot in skirmish.Slots)
            {
                int handicap = r.ReadByte(), credits = r.ReadByte();
                bool army = r.ReadBoolean();
                if (handicap < SimSetup.MinHandicap || handicap > 100) throw new InvalidDataException($"bad slot handicap {handicap}");
                if (credits >= SlotOptions.CreditsPresets.Count) throw new InvalidDataException($"bad slot credits preset {credits}");
                withOptions.Add(slot with { Handicap = handicap, CreditsPreset = credits, SmallArmy = army });
            }
            skirmish = new SkirmishSettings(skirmish.Map, withOptions, skirmish.SimSeed);
        }
        return new GameSaveHeader
        {
            Version = version, Mode = mode, MapId = mapId, MapName = mapName, RulesHash = rulesHash, LocalPlayer = local, Tick = tick,
            MissionId = missionId, ScriptHash = scriptHash, Build = build, Skirmish = skirmish, Ai = ai, Pending = pending, Sequences = sequences,
            ControlGroups = controlGroups,
        };
    }

    // Header version 2 skirmish settings: map choice (file or generator parameters), sim seed, then the slots.
    private static void WriteSettings(BinaryWriter w, SkirmishSettings s)
    {
        if (s.Map.File is { } file)
        {
            w.Write((byte)0);
            w.Write(file);
        }
        else
        {
            MapGenParams g = s.Map.Generated ?? throw new InvalidOperationException("The skirmish settings name no map.");
            w.Write((byte)1);
            w.Write((byte)g.Biome);
            w.Write((byte)g.Size);
            w.Write(g.Players);
            w.Write(g.Seed);
            w.Write(g.Name is not null);
            if (g.Name is not null) w.Write(g.Name);
        }
        w.Write(s.SimSeed);
        w.Write((byte)s.Slots.Count);
        foreach (SkirmishSlot slot in s.Slots)
        {
            w.Write((byte)slot.Kind);
            w.Write((byte)slot.Faction);
            w.Write(slot.AiTier);
            w.Write(slot.Colour);
            w.Write(slot.Team);
        }
    }

    private static SkirmishSettings ReadSettings(BinaryReader r)
    {
        byte mapKind = r.ReadByte();
        MapChoice map;
        if (mapKind == 0) map = new MapChoice(r.ReadString(), null);
        else if (mapKind == 1)
        {
            var biome = (Biome)r.ReadByte();
            var size = (MapSize)r.ReadByte();
            int players = r.ReadInt32();
            ulong seed = r.ReadUInt64();
            string? name = r.ReadBoolean() ? r.ReadString() : null;
            map = new MapChoice(null, new MapGenParams(biome, size, players, seed, name, OceanEdges.None, 0));   // layout 0 until the version-6 byte says otherwise (M12-05)
        }
        else throw new InvalidDataException($"unknown skirmish map kind {mapKind}");
        ulong simSeed = r.ReadUInt64();
        int count = r.ReadByte();
        if (count < 1 || count > SkirmishSettings.MaxSlots) throw new InvalidDataException($"bad skirmish slot count {count}");
        var slots = new List<SkirmishSlot>(count);
        for (int i = 0; i < count; i++)
            slots.Add(new SkirmishSlot((SlotKind)r.ReadByte(), (Faction)r.ReadByte(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
        return new SkirmishSettings(map, slots, simSeed);
    }

    private static SkirmishSettings ReadSettingsV1(BinaryReader r)
    {
        ulong seed = r.ReadUInt64();
        int tier = r.ReadInt32();
        int colour = r.ReadInt32();
        var pf = (Faction)r.ReadByte();
        var af = (Faction)r.ReadByte();
        return new SkirmishSettings(seed, tier, colour, pf, af);
    }
}
