// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tests.Rules;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Movement;

/// <summary>S41 M13-44: [movement] stuck_recovery, the ladder decision and the stuck clock in the locomotor.</summary>
public sealed class StuckRecoveryTests
{
    private readonly ITestOutputHelper _out;
    public StuckRecoveryTests(ITestOutputHelper output) => _out = output;

    // ---- rules key ----

    [Fact]
    public void ShippedRules_HaveStuckRecoveryOff_AndTheHashIsTheOldOne()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        RuleSet shipped = RulesLoader.Load(dir!);
        Assert.Equal(0, shipped.Sim.Movement.StuckRecovery);
        string game = File.ReadAllText(Path.Combine(dir!, "game.toml"));
        int at = game.IndexOf("[movement]", StringComparison.Ordinal);
        Assert.True(at > 0);
        Assert.Contains("stuck_recovery = 0", game[at..]);
        // the shipped files with and without the [movement] table: off, the rules hash is what it was before the key existed
        string copy = Path.Combine(AppContext.BaseDirectory, "rules-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(copy);
        try
        {
            foreach (string f in Directory.GetFiles(dir!, "*.toml")) File.Copy(f, Path.Combine(copy, Path.GetFileName(f)));
            File.WriteAllText(Path.Combine(copy, "game.toml"), game[..at]);
            Assert.Equal(RulesLoader.Load(copy).RulesHash, shipped.RulesHash);
        }
        finally { Directory.Delete(copy, true); }
    }

    [Fact]
    public void MovementTable_Parses_DefaultsWhenAbsent_RejectsBadInput_AndOnChangesTheHash()
    {
        string without = TempRules.Real("game.toml");   // the fixture file has no [movement] table
        using (var t = new TempRules(("game.toml", without))) Assert.Equal(MovementRules.Default, RulesLoader.Load(t.Dir).Sim.Movement);
        using var off = new TempRules(("game.toml", without + "[movement]\nstuck_recovery = 0\n"));
        using var on = new TempRules(("game.toml", without + "[movement]\nstuck_recovery = 1\n"));
        RuleSet ro = RulesLoader.Load(off.Dir), rn = RulesLoader.Load(on.Dir);
        Assert.Equal(0, ro.Sim.Movement.StuckRecovery);
        Assert.Equal(1, rn.Sim.Movement.StuckRecovery);
        Assert.NotEqual(ro.RulesHash, rn.RulesHash);
        using (var t = new TempRules(("game.toml", without + "[movement]\nbogus = 1\n"))) Assert.Throws<RulesException>(() => RulesLoader.Load(t.Dir));
        using (var t = new TempRules(("game.toml", without + "[movement]\nstuck_recovery = 2\n"))) Assert.Throws<RulesException>(() => RulesLoader.Load(t.Dir));
    }

    // ---- ladder decision ----

    [Fact]
    public void Ladder_FiresEachRungOnce_InOrder()
    {
        int rung = 0;
        Assert.Equal(StuckAction.None, StuckLadder.Decide(59, ref rung));
        Assert.Equal(StuckAction.Replan, StuckLadder.Decide(60, ref rung));
        Assert.Equal(StuckAction.None, StuckLadder.Decide(61, ref rung));
        Assert.Equal(StuckAction.Sidestep, StuckLadder.Decide(120, ref rung));
        Assert.Equal(StuckAction.None, StuckLadder.Decide(199, ref rung));
        Assert.Equal(StuckAction.Relax, StuckLadder.Decide(200, ref rung));
        Assert.Equal(StuckAction.Abandon, StuckLadder.Decide(320, ref rung));
        Assert.Equal(StuckAction.None, StuckLadder.Decide(5000, ref rung));
        Assert.Equal(4, rung);
    }

    [Fact]
    public void Ladder_AJumpSkipsToTheHighestDueRung()
    {
        int rung = 0;
        Assert.Equal(StuckAction.Relax, StuckLadder.Decide(250, ref rung));   // e.g. the unit was asleep: only the latest due rung acts
        Assert.Equal(StuckAction.Abandon, StuckLadder.Decide(330, ref rung));
    }

    // ---- corridor ----

    private static Simulation CorridorSim(int stuckRecovery, ulong seed = 1)
    {
        var map = MoveWorld.Grid(12, 3, (land, w) => { for (int x = 0; x < w; x++) { land[x] = LandType.Rock; land[2 * w + x] = LandType.Rock; } });
        VehicleType tank = ScriptedGame.Vehicle("tank", 40, 100);
        var rules = new SimRules(new[] { tank }, new[] { ScriptedGame.Infantry("rifle", 20, 30) },
            ScriptedGame.Buildings(), Array.Empty<WeaponType>(), Array.Empty<WarheadType>(), MoveWorld.Terrain(), Array.Empty<EffectDef>())
        { Movement = new MovementRules(stuckRecovery) };
        return new Simulation(new SimSetup(seed, map, 2, rules, 1) { AllowDebug = true });
    }

    private sealed record Outcome(string State, int LastMoveTickA, int LastMoveTickB, bool OrderLeftA, bool OrderLeftB, ulong Hash);

    /// <summary>Two own tanks head-on in a one-cell corridor, each ordered to the other's end.</summary>
    private Outcome RunCorridor(int stuckRecovery, int ticks, ulong seed = 1)
    {
        Simulation sim = CorridorSim(stuckRecovery, seed);
        EntityId a = sim.SpawnVehicle(0, 0, new CellCoord(3, 1));
        EntityId b = sim.SpawnVehicle(0, 0, new CellCoord(8, 1));
        int lastA = 0, lastB = 0;
        LeptonCoord pa = sim.Units.Position[a.Index], pb = sim.Units.Position[b.Index];
        for (int t = 0; t < ticks; t++)
        {
            Command[] cmds = t == 0
                ? new[] { MoveWorld.Move(a, ActorKind.Vehicle, 10, 1, 0, 0, 0), MoveWorld.Move(b, ActorKind.Vehicle, 1, 1, 0, 0, 1) }
                : MoveWorld.None;
            sim.Step(cmds);
            if (sim.Units.Position[a.Index] != pa) { pa = sim.Units.Position[a.Index]; lastA = t; }
            if (sim.Units.Position[b.Index] != pb) { pb = sim.Units.Position[b.Index]; lastB = t; }
            if (t % 100 == 99)
                _out.WriteLine($"rule {stuckRecovery} t={t + 1}: A {MoveWorld.CellOf(sim, a)} {sim.Units.Mission[a.Index]}/{sim.Units.Status[a.Index]} stuck {sim.Units.StuckCount[a.Index]}; "
                    + $"B {MoveWorld.CellOf(sim, b)} {sim.Units.Mission[b.Index]}/{sim.Units.Status[b.Index]} stuck {sim.Units.StuckCount[b.Index]}");
        }
        string state = $"A {MoveWorld.CellOf(sim, a)} B {MoveWorld.CellOf(sim, b)}";
        return new Outcome(state, lastA, lastB, sim.Units.Mission[a.Index] != MissionKind.Move, sim.Units.Mission[b.Index] != MissionKind.Move, sim.ComputeStateHash());
    }

    /// <summary>The wedge that stays frozen today (M13-16): the mover's goal cell is held for good by a unit that cannot get out of the way, so
    /// the mover waits awake beside it and nobody is ever told there is no way.</summary>
    private (int FirstNoWay, int NoWayCount, CellCoord End, ulong Hash) RunHeldGoal(int stuckRecovery, int ticks, ulong seed = 1)
    {
        Simulation sim = CorridorSim(stuckRecovery, seed);
        EntityId a = sim.SpawnVehicle(0, 0, new CellCoord(2, 1));
        EntityId b = sim.SpawnVehicle(0, 0, new CellCoord(6, 1));
        sim.Units.Mission[a.Index] = MissionKind.Submerge;   // a mission with no handler of its own: the test is the only caller
        sim.Units.Mission[b.Index] = MissionKind.Submerge;
        int first = -1, count = 0;
        for (int t = 0; t < ticks; t++)
        {
            if (MoveMission.StepToward(sim, sim.Units, ActorKind.Vehicle, a.Index, new CellCoord(6, 1)) >= MoveMission.WaitTicks) { count++; if (first < 0) first = t; }
            sim.Step(MoveWorld.None);
        }
        _out.WriteLine($"rule {stuckRecovery}: first no-way {first}, count {count}, A at {MoveWorld.CellOf(sim, a)}");
        return (first, count, MoveWorld.CellOf(sim, a), sim.ComputeStateHash());
    }

    [Fact]
    public void HeldGoal_RuleOn_TheLadderEndsTheWait_WithTheNoWayAnswer()
    {
        var r = RunHeldGoal(1, 800);
        Assert.InRange(r.FirstNoWay, StuckLadder.AbandonTicks, StuckLadder.AbandonTicks + 120);   // R4, counted from when the mover stopped
        Assert.True(r.NoWayCount >= 1);
    }

    [Fact]
    public void HeldGoal_RuleOff_IsTodaysBehaviour_FrozenForGood()
    {
        var r = RunHeldGoal(0, 800);
        Assert.Equal(0, r.NoWayCount);
        Assert.Equal(new CellCoord(5, 1), r.End);
    }

    [Fact]
    public void Corridor_Move_RuleOn_BothEndWithoutAnOrder()
    {
        Outcome o = RunCorridor(1, 1200);
        _out.WriteLine($"{o.State} lastMove A {o.LastMoveTickA} B {o.LastMoveTickB} orderLeft {o.OrderLeftA}/{o.OrderLeftB}");
        Assert.True(o.OrderLeftA && o.OrderLeftB);
    }

    [Fact]
    public void Corridor_Move_RuleOff_IsTodaysBehaviour_ItShufflesAndEndsTheSame()
    {
        Outcome off = RunCorridor(0, 1200), on = RunCorridor(1, 1200);
        Assert.True(off.OrderLeftA && off.OrderLeftB);
        Assert.Equal(off.State, on.State);   // a dynamic jam: the old logic already resolves it, the ladder must not make it worse
    }

    [Fact]
    public void RuleOn_StuckClockIsHashed_RuleOffIsNot()
    {
        foreach (int rule in new[] { 1, 0 })
        {
            Simulation sim = CorridorSim(rule);
            EntityId a = sim.SpawnVehicle(0, 0, new CellCoord(4, 1));
            ulong before = sim.ComputeStateHash();
            sim.Units.StuckAnchorTick[a.Index] = 99;
            sim.Units.StuckRung[a.Index] = 2;
            sim.Units.StuckAnchor[a.Index] = new CellCoord(3, 1);
            Assert.Equal(rule == 1, before != sim.ComputeStateHash());
        }
    }

    [Fact]
    public void RuleOn_SameSeedTwice_SameFinalHash()
    {
        Assert.Equal(RunCorridor(1, 400, 7).Hash, RunCorridor(1, 400, 7).Hash);
        Assert.Equal(RunHeldGoal(1, 600, 7).Hash, RunHeldGoal(1, 600, 7).Hash);
    }
}
