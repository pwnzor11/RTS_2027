// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests.M8;

/// <summary>A hook source the test sets by hand (the game layer's adapter reads the mission host instead).</summary>
internal sealed class FakeHooks : IMissionAiHooks
{
    public AiHooks Current = AiHooks.None;
    public AiHooks Read(byte player) => player == 0 ? Current : AiHooks.None;
    public DifficultyProfile? ProfileForTier(int tier) => tier >= 1 && tier <= TestWorld.Tiers.Count ? TestWorld.Tiers[tier - 1] : null;
}

/// <summary>M8-06: one Authority AI in slot 0 with a few foot soldiers, an idle enemy yard in slot 1, and a hook source.</summary>
internal sealed class HookRig
{
    private sealed class Recorder(IAiPlayer inner, HookRig rig) : IAiPlayer, IAiSnapshotable, IHookableAi
    {
        public void SaveState(BinaryWriter w) => ((IAiSnapshotable)inner).SaveState(w);
        public void LoadState(BinaryReader r) => ((IAiSnapshotable)inner).LoadState(r);
        public void ApplyHooks(AiHooks h, DifficultyProfile p) => ((IHookableAi)inner).ApplyHooks(h, p);
        public string Id => inner.Id;
        public void Start(AiContext ctx) => inner.Start(ctx);
        public void Think(PlayerView view, ICommandSink sink) => inner.Think(view, new Sink(rig, sink));

        private sealed class Sink(HookRig rig, ICommandSink real) : ICommandSink
        {
            public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0)
            {
                if (kind == CommandKind.AttackMove) rig.AttackMoves.Add(cell);
                if (kind == CommandKind.Move) rig.Moves.Add(cell);
                real.Issue(kind, subject, subjectKind, cell, target, arg2);
            }
            public void Build(ProductionCategory category, int typeIndex) => real.Build(category, typeIndex);
            public void Place(int buildingType, CellCoord topLeft) => real.Place(buildingType, topLeft);
            public void Rally(EntityId factory, CellCoord cell) => real.Rally(factory, cell);
        }
    }

    private readonly List<Command> _pending = new();
    private readonly DifficultyProfile _profile;
    private readonly bool _attach;
    private AiDriver _driver;

    public Simulation Sim { get; }
    public RecruitAi Ai { get; private set; }
    public FakeHooks Hooks { get; } = new();
    public List<CellCoord> AttackMoves { get; } = new();
    public List<CellCoord> Moves { get; } = new();

    public HookRig(int soldiers, int tier = 1, bool attachHooks = true, ulong seed = 1)
    {
        var rules = TestWorld.Rules;
        Sim = new Simulation(new SimSetup(seed, MapGenerator.Generate(64, seed), 2, rules.Sim, rules.RulesHash) { AllowDebug = true });
        SkirmishStart.Apply(Sim);
        SpawnFoot(soldiers);
        _profile = TestWorld.Tiers[tier - 1];
        _attach = attachHooks;
        _driver = Build(seed, out RecruitAi ai);
        Ai = ai;
    }

    private AiDriver Build(ulong seed, out RecruitAi ai)
    {
        var d = new AiDriver(seed);
        ai = new RecruitAi(_profile);
        d.Add(0, new Recorder(ai, this), _profile);
        if (_attach) d.SetMissionHooks(Hooks);
        return d;
    }

    public CellCoord Yard()
    {
        for (int i = 1; i < Sim.Buildings.Capacity; i++)
            if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == 0) return Sim.Buildings.Cell[i];
        throw new InvalidOperationException();
    }

    private void SpawnFoot(int count)
    {
        int type = TestWorld.Rules.Sim.Infantry.ToList().FindIndex(v => v.Id == "auth_rifleman");
        CellCoord near = Yard();
        int spawned = 0;
        for (int r = 2; r <= 12 && spawned < count; r++)
            for (int dy = -r; dy <= r && spawned < count; dy++)
                for (int dx = -r; dx <= r && spawned < count; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(near.X + dx, near.Y + dy);
                    if (!Sim.Setup.Map.Contains(c) || !Sim.Map.BuildingAt(c).IsNone) continue;
                    if (!Sim.SpawnInfantry(0, type, c).IsNone) spawned++;
                }
        if (spawned != count) throw new InvalidOperationException("no room for soldiers");
    }

    /// <summary>A passable cell at least 14 cells from the yard (searched row by row, so it is the same for equal seeds).</summary>
    public CellCoord FarCell()
    {
        CellCoord y = Yard();
        PlayerView v = Sim.ViewFor(0);
        for (int dist = 14; dist < 30; dist++)
            for (int dx = -dist; dx <= dist; dx += 2)
                foreach (int dy in new[] { -dist, dist })
                {
                    var c = new CellCoord(y.X + dx, y.Y + dy);
                    if (c.X > 2 && c.Y > 2 && c.X < 60 && c.Y < 60 && v.IsPassable(c, Locomotion.Tracked)) return c;
                }
        throw new InvalidOperationException("no far cell");
    }

    public void Run(int ticks)
    {
        for (int t = 0; t < ticks; t++)
        {
            _driver.Collect(Sim, _pending);
            Sim.Step(_pending);
            _pending.RemoveAll(c => c.ExecTick < Sim.Tick);
        }
    }

    public byte[]? Capture() => _driver.CaptureState(0);

    public ulong ResumeWithBlob(byte[] blob, int ticks)
    {
        _driver = Build(Sim.Setup.Seed, out RecruitAi ai);
        Ai = ai;
        _driver.RestoreState(0, blob);
        Run(ticks);
        return Sim.ComputeStateHash();
    }
}
