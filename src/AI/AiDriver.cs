// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>
/// Hosts AI players outside the simulation (D-051). The host calls <see cref="Collect"/> once per tick BEFORE
/// Simulation.Step and feeds the produced commands into the recorded command stream. The driver calls Think every
/// <see cref="DifficultyProfile.DecisionIntervalTicks"/>, stamps
/// ExecTick = tick + command delay (mirrors LAN input delay) and a per-player increasing Sequence. AI memory lives in
/// the AI objects, not in the sim. No wall clock, no unordered iteration: the same seed gives the same commands.
/// </summary>
public sealed class AiDriver
{
    private sealed class Slot : ICommandSink
    {
        public required byte Player;
        public required IAiPlayer Ai;
        public required DifficultyProfile Profile;
        public ushort Sequence;
        public int Delay;
        public int Tick;
        public List<Command> Out = null!;
        public byte[]? Saved;   // M3-21: a state blob from a save, applied once when the driver starts
        public CellCoord? SavedStart;
        public IDirectiveSource? Directives;   // M5: the LLM commander, polled every tick

        public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default,
            ActorRef target = default, int arg2 = 0)
        {
            int arg = (int)subjectKind;
            if (kind is CommandKind.Attack or CommandKind.Enter) arg |= ((int)target.Kind & 0xFF) << 8;
            Out.Add(new Command(Tick + Delay, Player, unchecked(Sequence++), kind, subject, target.Id, cell, arg, arg2));
        }

        public void Build(ProductionCategory category, int typeIndex) =>
            Out.Add(new Command(Tick + Delay, Player, unchecked(Sequence++), CommandKind.Build, EntityId.None, EntityId.None, default,
                (int)category, typeIndex));

        public void Place(int buildingType, CellCoord topLeft) =>
            Out.Add(new Command(Tick + Delay, Player, unchecked(Sequence++), CommandKind.Place, EntityId.None, EntityId.None, topLeft,
                buildingType));

        public void Rally(EntityId factory, CellCoord cell) =>
            Out.Add(new Command(Tick + Delay, Player, unchecked(Sequence++), CommandKind.Rally, factory, EntityId.None, cell,
                (int)ActorKind.Building));
    }

    private readonly ulong _seed;
    private readonly List<Slot> _slots = new();
    private bool _started;

    /// <summary>Creates a driver for a game with the given seed (AI generators derive from it).</summary>
    public AiDriver(ulong gameSeed) => _seed = gameSeed;

    /// <summary>When set, replaces every profile's command delay (ticks, at least 1).</summary>
    public int? CommandDelayOverride { get; set; }

    /// <summary>Number of AI slots.</summary>
    public int Count => _slots.Count;

    /// <summary>Gives a player slot to an AI. Call before the first Collect; one AI per slot.</summary>
    public void Add(byte player, IAiPlayer ai, DifficultyProfile profile)
    {
        if (_started) throw new InvalidOperationException("Add AIs before the first Collect.");
        foreach (Slot s in _slots)
            if (s.Player == player) throw new ArgumentException($"Slot {player} already has an AI.", nameof(player));
        _slots.Add(new Slot { Player = player, Ai = ai, Profile = profile });
        _slots.Sort((a, b) => a.Player.CompareTo(b.Player));
    }

    /// <summary>Runs due AIs against the sim as it stands and appends their commands to <paramref name="outCommands"/>.
    /// Call once per tick, before Step.</summary>
    public void Collect(Simulation sim, List<Command> outCommands)
    {
        if (!_started) Start(sim);
        int tick = sim.Tick;
        foreach (Slot s in _slots)
        {
            if (s.Directives is not null && s.Directives.Poll(sim.ViewFor(s.Player), out int packed, out CellCoord focus))   // M5-05
            {
                int delay = Math.Max(1, CommandDelayOverride ?? s.Profile.CommandDelayTicks);
                outCommands.Add(new Command(tick + delay, s.Player, unchecked(s.Sequence++), CommandKind.Directive, EntityId.None, EntityId.None,
                    focus, packed));
            }
            int interval = Math.Max(1, s.Profile.DecisionIntervalTicks);
            if (tick % interval != 0) continue;   // D-065: all slots think on the same tick (a per-slot stagger biased mirror matches)
            s.Tick = tick;
            s.Delay = Math.Max(1, CommandDelayOverride ?? s.Profile.CommandDelayTicks);
            s.Out = outCommands;
            s.Ai.Think(sim.ViewFor(s.Player), s);
            s.Out = null!;
        }
    }

    private void Start(Simulation sim)
    {
        _started = true;
        var own = new List<ViewActor>();
        foreach (Slot s in _slots)
        {
            PlayerView view = sim.ViewFor(s.Player);
            view.CopyOwn(own);
            long sx = 0, sy = 0;
            foreach (ViewActor a in own) { sx += a.Cell.X; sy += a.Cell.Y; }
            var start = own.Count == 0 ? new CellCoord(view.MapWidth / 2, view.MapHeight / 2)
                : new CellCoord((int)(sx / own.Count), (int)(sy / own.Count));
            if (s.Saved is not null && TryReadSaved(s.Saved, out ushort savedSeq, out CellCoord savedStart, out byte[] aiBytes))
            {
                s.Sequence = savedSeq;
                start = savedStart;   // the AI's home / scout route came from the first-ever start cell, not from where its units are now
                s.Saved = aiBytes;
            }
            else s.Saved = null;
            s.SavedStart = start;   // M3-21: a later save writes it back
            s.Ai.Start(new AiContext
            {
                Player = s.Player, PlayerCount = view.PlayerCount, GameSeed = _seed, Profile = s.Profile,
                Rng = new AiRandom(_seed, s.Player), MapWidth = view.MapWidth, MapHeight = view.MapHeight, StartCell = start,
                StartPositions = sim.Setup.Map.StartPositions,   // M4-12: public map data (scouting for 2-4 starts)
            });
            if (s.Saved is { Length: > 0 } && s.Ai is IAiSnapshotable restorable)
            {
                try { using var br = new BinaryReader(new MemoryStream(s.Saved)); restorable.LoadState(br); }
                catch (Exception e) when (e is EndOfStreamException or InvalidDataException or InvalidOperationException or IOException)
                {
                    Restored = false;   // a half-read AI is not trusted: the caller can see it and tell the player
                }
            }
            s.Saved = null;
        }
    }

    // ---- M3-21: AI save / restore --------------------------------------------------------------------------------------

    /// <summary>M3-21: false when a saved AI state was found unreadable while starting (that AI then runs on partly restored state; see
    /// <see cref="AiSnapshot"/>). Stays true otherwise, also when nothing was restored.</summary>
    public bool Restored { get; private set; } = true;

    /// <summary>
    /// M3-21: the AI memory of slot <paramref name="player"/> as bytes for a save, or null when the slot has no AI or its AI keeps nothing
    /// (it then restarts fresh). Holds the slot's command sequence, its start cell and the AI's own state (<see cref="IAiSnapshotable"/>).
    /// Call between ticks; before the first Collect there is nothing to save.
    /// </summary>
    public byte[]? CaptureState(byte player)
    {
        foreach (Slot s in _slots)
        {
            if (s.Player != player) continue;
            if (!_started) return s.Saved is null ? null : AiSnapshot.Pack(s.Saved);   // loaded and saved again before the first tick: hand it on
            if (s.Ai is not IAiSnapshotable snap) return null;
            using var ms = new MemoryStream();
            using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                w.Write(s.Sequence);
                CellCoord start = s.SavedStart ?? default;
                w.Write(start.X); w.Write(start.Y);
                snap.SaveState(w);
            }
            return AiSnapshot.Pack(ms.ToArray());
        }
        return null;
    }

    /// <summary>
    /// M3-21: hands a blob from <see cref="CaptureState"/> to slot <paramref name="player"/>; it is applied when the driver starts (the first
    /// Collect), after the AI's Start. A damaged blob, one of another format version or one for an unknown slot is ignored (the AI starts fresh).
    /// Call before the first Collect.
    /// </summary>
    public void RestoreState(byte player, byte[]? blob)
    {
        if (_started || blob is null) return;
        foreach (Slot s in _slots)
            if (s.Player == player && AiSnapshot.TryUnpack(blob, out byte[] payload)) s.Saved = payload;
    }

    /// <summary>M5: gives slot <paramref name="player"/> a directive source (the LLM commander); null removes it. The slot must already
    /// have an AI (<see cref="Add"/>). May be called at any time between ticks.</summary>
    public void SetDirectiveSource(byte player, IDirectiveSource? source)
    {
        foreach (Slot s in _slots)
            if (s.Player == player) { s.Directives = source; return; }
        throw new ArgumentException($"Slot {player} has no AI.", nameof(player));
    }

    /// <summary>M3-21 (diagnostics and tests): the AI playing slot <paramref name="player"/>, or null.</summary>
    public IAiPlayer? AiOf(byte player)
    {
        foreach (Slot s in _slots) if (s.Player == player) return s.Ai;
        return null;
    }

    private static bool TryReadSaved(byte[] payload, out ushort sequence, out CellCoord start, out byte[] aiBytes)
    {
        sequence = 0; start = default; aiBytes = Array.Empty<byte>();
        if (payload.Length < 10) return false;
        using var r = new BinaryReader(new MemoryStream(payload));
        sequence = r.ReadUInt16();
        start = new CellCoord(r.ReadInt32(), r.ReadInt32());
        aiBytes = r.ReadBytes(payload.Length - 10);
        return true;
    }
}
