// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
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

        public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default,
            ActorRef target = default, int arg2 = 0)
        {
            int arg = (int)subjectKind;
            if (kind == CommandKind.Attack) arg |= ((int)target.Kind & 0xFF) << 8;
            Out.Add(new Command(Tick + Delay, Player, unchecked(Sequence++), kind, subject, target.Id, cell, arg, arg2));
        }
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
            s.Ai.Start(new AiContext
            {
                Player = s.Player, PlayerCount = view.PlayerCount, GameSeed = _seed, Profile = s.Profile,
                Rng = new AiRandom(_seed, s.Player), MapWidth = view.MapWidth, MapHeight = view.MapHeight, StartCell = start,
            });
        }
    }
}
