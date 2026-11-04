// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
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
    /// <summary>The header version this build writes and reads. Bump it when the layout changes. Version 1 (two-player skirmish settings) is still read; M4-11 wrote version 2 (map choice, 2 to 4 slots).</summary>
    public const int CurrentVersion = 2;

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

    /// <summary>Build version that wrote the save.</summary>
    public string Build { get; init; } = "";

    /// <summary>One line for the load list.</summary>
    public string Describe()
    {
        double minutes = Tick / (double)SimConstants.TicksPerSecond / 60.0;
        string time = FormattableString.Invariant($"{minutes:0.0} min");
        if (Mode == SaveMode.Mission) return $"Mission {MissionId}, {time}";
        string ai = Ai.Count > 0 ? $", vs {Ai[0].AiId} tier {Ai[0].Tier}" : "";
        return $"Skirmish on {MapName}{ai}, {time}";
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
            ai.Add(new SavedAi(r.ReadByte(), r.ReadString(), r.ReadInt32(), (Faction)r.ReadByte(), r.ReadUInt64()));
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
        return new GameSaveHeader
        {
            Version = version, Mode = mode, MapId = mapId, MapName = mapName, RulesHash = rulesHash, LocalPlayer = local, Tick = tick,
            MissionId = missionId, ScriptHash = scriptHash, Build = build, Skirmish = skirmish, Ai = ai, Pending = pending, Sequences = sequences,
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
            map = new MapChoice(null, new MapGenParams(biome, size, players, seed, name));
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
