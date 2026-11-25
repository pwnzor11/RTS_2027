// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S41;

/// <summary>s41 (M13-45): committed AI waves. <c>wave_reinforce_ticks</c> calls idle troops at home into a wave that is out; <c>wave_search</c> makes a wave that
/// knows no enemy sweep the map sectors. Both default off and then change nothing.</summary>
public sealed class WaveCommitTests
{
    /// <summary>A one-AI game: slot 0 plays the AI on a flat map; slot 1 is emptied at tick 0 (nothing to find, nothing to fear).</summary>
    private sealed class WaveRig
    {
        private ushort _seq = 51000;
        private readonly List<Command> _pending = new();
        private AiDriver _driver;
        private readonly DifficultyProfile _profile;
        public Simulation Sim { get; }
        public RecruitAi Ai { get; private set; }

        public WaveRig(DifficultyProfile profile, int army, bool emptyEnemy)
        {
            Sim = new Simulation(new SimSetup(1, MapGenerator.Generate(64, 1), 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash) { AllowDebug = true });
            SkirmishStart.Apply(Sim);
            int foot = SimRules.IndexOf(TestWorld.Rules.Sim.Infantry, "auth_rifleman", v => v.Id);
            CellCoord yard = Yard(0);
            int spawned = 0;
            for (int r = 3; r <= 12 && spawned < army; r++)
                for (int dy = -r; dy <= r && spawned < army; dy++)
                    for (int dx = -r; dx <= r && spawned < army; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var c = new CellCoord(yard.X + dx, yard.Y + dy);
                        if (Sim.Setup.Map.Contains(c) && Sim.Map.BuildingAt(c).IsNone && !Sim.SpawnInfantry(0, foot, c).IsNone) spawned++;
                    }
            Assert.Equal(army, spawned);
            if (emptyEnemy)
            {
                for (int i = 1; i < Sim.Buildings.Capacity; i++)
                    if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == 1) Kill(Sim.Buildings.Slots.IdOf(i), ActorKind.Building);
                for (int i = 1; i < Sim.Units.Capacity; i++)
                    if (Sim.Units.Slots.IsIndexAlive(i) && Sim.Units.Player[i] == 1) Kill(Sim.Units.Slots.IdOf(i), ActorKind.Vehicle);
                for (int i = 1; i < Sim.Infantry.Capacity; i++)
                    if (Sim.Infantry.Slots.IsIndexAlive(i) && Sim.Infantry.Player[i] == 1) Kill(Sim.Infantry.Slots.IdOf(i), ActorKind.Infantry);
            }
            _profile = profile;
            _driver = new AiDriver(1);
            Ai = new RecruitAi(profile);
            _driver.Add(0, Ai, profile);
        }

        private CellCoord Yard(byte player)
        {
            for (int i = 1; i < Sim.Buildings.Capacity; i++)
                if (Sim.Buildings.Slots.IsIndexAlive(i) && Sim.Buildings.Player[i] == player) return Sim.Buildings.Cell[i];
            throw new InvalidOperationException("no building");
        }

        private void Kill(EntityId id, ActorKind kind) =>
            _pending.Add(new Command(0, 1, _seq++, CommandKind.Debug, id, default, default, (int)DebugOp.Kill, (int)kind));

        public void SpawnFootAt(CellCoord cell) =>
            _pending.Add(new Command(Sim.Tick, 0, _seq++, CommandKind.Debug, EntityId.None, default, cell, (int)DebugOp.SpawnInfantry,
                SimRules.IndexOf(TestWorld.Rules.Sim.Infantry, "auth_rifleman", v => v.Id)));

        public void Run(int ticks, Func<bool>? until = null)
        {
            for (int t = 0; t < ticks; t++)
            {
                _driver.Collect(Sim, _pending);
                Sim.Step(_pending);
                _pending.RemoveAll(c => c.ExecTick < Sim.Tick);
                if (until != null && until()) return;
            }
        }

        public ulong ResumeWithBlob(byte[] blob, int ticks)
        {
            _driver = new AiDriver(1);
            Ai = new RecruitAi(_profile);
            _driver.Add(0, Ai, _profile);
            _driver.RestoreState(0, blob);
            Run(ticks);
            return Sim.ComputeStateHash();
        }

        public byte[]? Capture() => _driver.CaptureState(0);
    }

    private static DifficultyProfile Tier => TestWorld.Tiers[2];

    // (a) keys off: the same game as at the base commit d8aa752 (hash pinned there before the change)
    [Fact]
    public void KeysOffGiveTheSameGameAsBefore()
    {
        Assert.Equal(0, Tier.WaveReinforceTicks);
        Assert.False(Tier.WaveSearch);
        int waves = 0;
        AiGameResult r = AiMatchRunner.Play(MapGenerator.Generate(32, 3), TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 3,
            new AiSide("recruit", Tier), new AiSide("recruit", Tier), true, 9000, start: AiStartMode.Army,
            observe: (s, a, b, pa, pb) => waves = ((RecruitAi)a).Waves + ((RecruitAi)b).Waves);
        Assert.True(waves >= 1);   // the game really sent waves
        // A43 (D-311) re-pin: 4844 / 6724706220407738216 -> 3344 / 8381130536209904247 (approved SIMECON/SIMCORE behaviour changes move this pinned game)
        // s44 A-02 re-pin (focus_fire now also drives the Recruit AI wave at tier 3): 3344 / 0 / 8381130536209904247 -> 3804 / 1 / 17843251954667786593; S44 merge (new state parts and part versions move the hash only): -> 4154896936043175216
        Assert.Equal(3804, r.EndTick);
        Assert.Equal(1, r.Winner);
        Assert.Equal(4154896936043175216UL, r.FinalHash);
    }

    // (b) reinforce on: a unit idle at the rally when the call-up falls due joins the roster
    [Fact]
    public void AnIdleUnitAtHomeJoinsTheWaveAtTheCallUp()
    {
        var rig = new WaveRig(Tier with { WaveReinforceTicks = 600 }, 30, emptyEnemy: true);
        rig.Run(3000, () => rig.Ai.Military.PhaseNumber != 0 && rig.Ai.RallyCell != default);
        Assert.NotEqual(0, rig.Ai.Military.PhaseNumber);
        int launch = rig.Sim.Tick;
        rig.SpawnFootAt(rig.Ai.RallyCell);
        rig.Run(5);
        int before = rig.Ai.Military.RosterCount;
        Assert.Equal(0, rig.Ai.Military.CalledUp);   // not due yet: the new unit waits
        rig.Run(700, () => rig.Ai.Military.CalledUp > 0);
        Assert.True(rig.Ai.Military.CalledUp >= 1);
        Assert.True(rig.Sim.Tick - launch >= 590);   // and only at the due tick
        Assert.True(rig.Ai.Military.RosterCount >= before + 1);
    }

    [Fact]
    public void WithReinforceOffNobodyIsCalledUp()
    {
        var rig = new WaveRig(Tier, 30, emptyEnemy: true);
        rig.Run(3000, () => rig.Ai.Military.PhaseNumber != 0 && rig.Ai.RallyCell != default);
        rig.SpawnFootAt(rig.Ai.RallyCell);
        rig.Run(1500);
        Assert.Equal(0, rig.Ai.Military.CalledUp);
        Assert.False(rig.Ai.Military.WaveDirty);
    }

    // (c) search on: with no known enemy the wave heads for a sector cell, and the goal changes once that sector is visited
    [Fact]
    public void AWaveThatKnowsNoEnemyHeadsForSectorCellsOneAfterAnother()
    {
        var rig = new WaveRig(Tier with { WaveSearch = true }, 30, emptyEnemy: true);
        rig.Run(6000, () => rig.Ai.Military.SearchGoal is not null);
        CellCoord first = rig.Ai.Military.SearchGoal ?? throw new Xunit.Sdk.XunitException("the wave never started to search");
        Assert.Equal(2, rig.Ai.Military.PhaseNumber);
        int sector = first.Y / 8 * 8 + first.X / 8;   // 64-cell map: 8 x 8 cells per sector
        Assert.InRange(sector, 0, 63);
        int visitedAtStart = rig.Ai.Military.SectorsVisited;
        rig.Run(4000, () => rig.Ai.Military.SearchGoal is { } g && g != first);
        CellCoord next = rig.Ai.Military.SearchGoal ?? throw new Xunit.Sdk.XunitException("the search ended after one sector");
        Assert.NotEqual(first, next);
        Assert.True(rig.Ai.Military.SectorsVisited > visitedAtStart);   // the first one was crossed off
        Assert.True(rig.Ai.Military.WaveDirty);
    }

    [Fact]
    public void WithSearchOffNoSectorIsEverChosen()
    {
        var rig = new WaveRig(Tier, 30, emptyEnemy: true);
        rig.Run(4000);
        Assert.Null(rig.Ai.Military.SearchGoal);
        Assert.Equal(0, rig.Ai.Military.SectorsVisited);
    }

    // (d) save and restore with the new fields filled
    [Fact]
    public void AnAiSavedWhileSearchingAndCallingUpContinuesLikeOneThatNeverSaved()
    {
        DifficultyProfile p = Tier with { WaveSearch = true, WaveReinforceTicks = 400 };
        const int After = 1200;
        var probe = new WaveRig(p, 30, emptyEnemy: true);
        probe.Run(8000, () => probe.Ai.Military.SearchGoal is not null);
        int before = probe.Sim.Tick;
        Assert.NotNull(probe.Ai.Military.SearchGoal);
        var straight = new WaveRig(p, 30, emptyEnemy: true);
        straight.Run(before + After);
        var r = new WaveRig(p, 30, emptyEnemy: true);
        r.Run(before);
        Assert.True(r.Ai.Military.WaveDirty);
        byte[]? blob = r.Capture();
        Assert.NotNull(blob);
        Assert.Equal(straight.Sim.ComputeStateHash(), r.ResumeWithBlob(blob!, After));
    }
}
