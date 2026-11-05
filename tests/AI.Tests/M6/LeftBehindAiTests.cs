// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;
using Rts.AI.Recruit;

namespace AI.Tests.M6;

/// <summary>M6-10: the Left Behind Raider Warlord plays headless: it unpacks and builds a base, salvages a wreck, raids a silo, relocates
/// under a heavy attack, and its memory survives a save and load.</summary>
public sealed class LeftBehindAiTests(ITestOutputHelper log)
{
    private static DifficultyProfile Tier(int tier) => TestWorld.Tiers[tier - 1];

    private static int Own(TierRig r, byte player, string vehicleId)
    {
        int type = r.VehicleType(vehicleId), n = 0;
        for (int i = 1; i < r.Sim.Units.Capacity; i++)
            if (r.Sim.Units.Slots.IsIndexAlive(i) && r.Sim.Units.Player[i] == player && r.Sim.Units.TypeIndex[i] == type) n++;
        return n;
    }

    private static CellCoord Anchor(TierRig r, byte player)
    {
        int type = r.BuildingType("lb_mother_rig");
        for (int i = 1; i < r.Sim.Buildings.Capacity; i++)
            if (r.Sim.Buildings.Slots.IsIndexAlive(i) && r.Sim.Buildings.Player[i] == player && r.Sim.Buildings.TypeIndex[i] == type) return r.Sim.Buildings.Cell[i];
        return default;
    }

    [Fact]
    public void ALeftBehindAiUnpacksItsRigAndBuildsABase()
    {
        var rig = new TierRig(Faction.LeftBehind, Tier(2) with { AttackThreshold = 500 });
        rig.Run(3000);
        log.WriteLine($"unpacks {rig.Ai.Unpacks}, placed {rig.Ai.StructuresPlaced}, unplaceable {rig.Ai.Unplaceable}, queued {rig.Ai.UnitsQueued}");
        Assert.True(rig.Ai.Unpacks >= 1, "the packed Mother Rig was ordered to unpack");
        Assert.Equal(1, rig.Count(0, "lb_mother_rig"));
        Assert.True(rig.Count(0, "lb_yard") >= 1, "the Yard stands");
        Assert.True(rig.Count(0, "lb_dynamo_rig") >= 1, "power stands");
        Assert.True(rig.Count(0, "lb_muster_rig") >= 1, "infantry production stands");
        Assert.True(rig.Count(0, "lb_garage_rig") >= 1, "vehicle production stands");
        Assert.Equal(0, rig.Ai.Unplaceable);
        Assert.True(rig.Ai.UnitsQueued >= 2, "units were queued");
    }

    [Fact]
    public void AHaulerSalvagesAScriptedWreck()
    {
        var rig = new TierRig(Faction.LeftBehind, Tier(2) with { AttackThreshold = 500 });
        rig.Run(2200);   // the base stands
        CellCoord a = Anchor(rig, 0);
        Assert.NotEqual(default, a);
        int hauler = rig.VehicleType("lb_hauler");
        EntityId h = TestWorld.LateVehicle(rig.Sim, 0, hauler, new CellCoord(a.X + 6, a.Y + 6));
        Assert.False(h.IsNone);
        EntityId victim = TestWorld.LateVehicle(rig.Sim, 1, rig.VehicleType("auth_tank"), new CellCoord(a.X + 9, a.Y + 9));
        Assert.False(victim.IsNone);
        rig.Kill(victim, ActorKind.Vehicle);
        rig.Run(3);
        var wrecks = new List<Rts.Sim.View.WreckView>();
        rig.Sim.ViewFor(0).CopyWrecks(wrecks);
        Assert.NotEmpty(wrecks);
        int before = wrecks.Sum(w => w.Value);
        rig.Run(1200, () => rig.Ai.SalvageOrders >= 1);
        rig.Run(600);
        rig.Sim.ViewFor(0).CopyWrecks(wrecks);
        int after = wrecks.Sum(w => w.Value);
        log.WriteLine($"salvage orders {rig.Ai.SalvageOrders}, wreck value {before} -> {after}");
        Assert.True(rig.Ai.SalvageOrders >= 1, "the idle Hauler was sent to the wreck");
        Assert.True(after < before, "ash was drained from the wreck");
    }

    /// <summary>Session 10: the AI buys its first Hauler without waiting for wrecks, and sends it to a fresh wreck even while it
    /// is out on the ash field.</summary>
    [Fact]
    public void ATierTwoAiBuysAnEarlyHaulerAndSalvagesAFreshWreck()
    {
        var rig = new TierRig(Faction.LeftBehind, Tier(2) with { AttackThreshold = 5000 });
        rig.Run(6000, () => Own(rig, 0, "lb_hauler") >= 1);
        Assert.True(Own(rig, 0, "lb_hauler") >= 1, "a Hauler was built with no wreck about");
        CellCoord a = Anchor(rig, 0);
        EntityId victim = TestWorld.LateVehicle(rig.Sim, 1, rig.VehicleType("auth_tank"), new CellCoord(a.X + 8, a.Y + 8));
        Assert.False(victim.IsNone);
        rig.Kill(victim, ActorKind.Vehicle);
        rig.Run(3);
        var wrecks = new List<Rts.Sim.View.WreckView>();
        rig.Sim.ViewFor(0).CopyWrecks(wrecks);
        int before = wrecks.Sum(w => w.Value), orders = rig.Ai.SalvageOrders;
        rig.Run(1000);
        rig.Sim.ViewFor(0).CopyWrecks(wrecks);
        log.WriteLine($"salvage orders {orders} -> {rig.Ai.SalvageOrders}, wreck value {before} -> {wrecks.Sum(w => w.Value)}, harvested {rig.Sim.StatsOf(0).CreditsHarvested}");
        Assert.True(rig.Ai.SalvageOrders > orders, "the Hauler was sent to the wreck");
        Assert.True(wrecks.Sum(w => w.Value) < before, "ash was drained from the wreck");
    }

    /// <summary>Session 10: in a real match (tier 4 against a tier 1 Left Behind, seed 1 as the `ai match` tool plays it) the AI builds
    /// the Second Drift, waits for the charge and fires it; the storm was never seen in a match before the session-9 entrance fix.</summary>
    [Fact]
    public void TheSecondDriftIsFiredInARealMatch()
    {
        MapData map = MapGenerator.Generate(56, 1);
        var a = new AiSide(AiRegistry.TieredId, Tier(4), Faction.LeftBehind);
        var b = new AiSide(AiRegistry.TieredId, Tier(1), Faction.LeftBehind);
        int storms = 0;
        AiGameResult r = AiMatchRunner.Play(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, 16, a, b, aInSlot0: true, maxTicks: 16000,
            observe: (sim, ai0, ai1, slotA, slotB) => storms = ((RecruitAi)ai0).StormOrders);
        log.WriteLine($"end tick {r.EndTick}, winner {r.Winner}, Second Drift orders {storms}");
        Assert.True(storms >= 1, "the Second Drift was fired in the match");
    }

    [Fact]
    public void RaidersGoForAnEnemySilo()
    {
        var rig = new TierRig(Faction.LeftBehind, Tier(2) with { AttackThreshold = 500 }, r =>
        {
            CellCoord enemy = r.Yard(1);
            r.PlaceNear(1, "auth_silo", enemy);
            CellCoord start = r.Sim.Setup.Map.StartPositions[0];
            for (int k = 0; k < 4; k++)
                Assert.False(r.Sim.SpawnVehicle(0, r.VehicleType("lb_buggy"), new CellCoord(start.X + 6 + k, start.Y + 8)).IsNone);
        });
        rig.Run(4000);
        log.WriteLine($"raid orders {rig.Ai.RaidOrders}, enemy cash {rig.Sim.Players.Cash[1]}");
        Assert.True(rig.Ai.RaidOrders >= 1, "the buggies were sent out");
        Assert.True(rig.Issue(CommandKind.Enter) + rig.Issue(CommandKind.AttackMove) >= 1);
    }

    [Fact]
    public void UnderAHeavyAttackATierThreeAiPacksAndMovesItsRigs()
    {
        var rig = new TierRig(Faction.LeftBehind, Tier(3) with { AttackThreshold = 500 });
        rig.Run(2600);
        CellCoord a = Anchor(rig, 0);
        Assert.NotEqual(default, a);
        for (int k = 0; k < 12; k++)
            TestWorld.LateVehicle(rig.Sim, 1, rig.VehicleType("auth_tank"), new CellCoord(a.X + 4 + k % 6, a.Y + 6 + k / 6));
        rig.Run(1500, () => rig.Ai.Relocations >= 1);
        log.WriteLine($"{rig.Ai.LeftBehindDebug}; relocations {rig.Ai.Relocations}, done {rig.Ai.RelocationsDone}, tick {rig.Sim.Tick}");
        Assert.True(rig.Ai.Relocations >= 1, "the heavy attack made the AI pack up");
        rig.Run(4200, () => rig.Ai.RelocationsDone >= 1 || rig.Ai.RelocationsAborted >= 1);
        Assert.True(rig.Ai.RelocationsDone + rig.Ai.RelocationsAborted >= 1, "the relocation ended one way or the other");
        log.WriteLine($"after: done {rig.Ai.RelocationsDone}, aborted {rig.Ai.RelocationsAborted}, tick {rig.Sim.Tick}, rig at {Anchor(rig, 0)}");
    }

    [Fact]
    public void ALowTierAiNeverRelocates()
    {
        var rig = new TierRig(Faction.LeftBehind, Tier(2) with { AttackThreshold = 500 });
        rig.Run(2600);
        CellCoord a = Anchor(rig, 0);
        for (int k = 0; k < 3; k++)
            TestWorld.LateVehicle(rig.Sim, 1, rig.VehicleType("auth_tank"), new CellCoord(a.X + 7 + k, a.Y + 7));
        rig.Run(800);
        Assert.Equal(0, rig.Ai.Relocations);
    }

    [Fact]
    public void ALeftBehindAiSavedMidGameContinuesLikeOneThatNeverSaved()
    {
        const int Before = 1500, After = 1500;
        ulong Straight()
        {
            var r = new TierRig(Faction.LeftBehind, Tier(3));
            r.Run(Before + After);
            return r.Sim.ComputeStateHash();
        }
        ulong resumed;
        {
            var r = new TierRig(Faction.LeftBehind, Tier(3));
            r.Run(Before);
            byte[]? blob = r.CaptureAi();
            Assert.NotNull(blob);
            ulong h = r.ResumeWithBlob(blob!, After);
            resumed = h;
        }
        Assert.Equal(Straight(), resumed);
    }

    /// <summary>Plays Left Behind (slot 0) against a tiered Authority and reports what the faction extras did; no rig may end the game packed and idle.</summary>
    [Theory]
    [InlineData(3, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    public void ALeftBehindAiPlaysAFullGameWithoutAStuckRig(int tier, ulong seed)
    {
        SimRules rules = TestWorld.Rules.Sim;
        MapData map = MapGenerator.Generate(56, seed);
        var sim = new Simulation(new SimSetup(seed, map, 2, rules, TestWorld.Rules.RulesHash) { Factions = [Faction.LeftBehind, Faction.Authority] });
        SkirmishStart.Apply(sim);
        var driver = new AiDriver(seed);
        var lb = new RecruitAi(Tier(tier));
        driver.Add(0, lb, Tier(tier));
        AiRegistry.TryCreate(AiRegistry.TieredId, Tier(3), out IAiPlayer? foe);
        driver.Add(1, foe!, Tier(3));
        var pending = new List<Command>();
        int idlePacked = 0, worst = 0;
        for (int t = 0; t < 14000 && !sim.Players.Defeated[0] && !sim.Players.Defeated[1]; t++)
        {
            driver.Collect(sim, pending);
            sim.Step(pending);
            pending.RemoveAll(c => c.ExecTick < sim.Tick);
            if (sim.Tick % 20 != 0) continue;
            var view = sim.ViewFor(0);
            var own = new List<Rts.Sim.View.ViewActor>();
            view.CopyOwn(own);
            bool anyIdlePacked = false;
            foreach (var a in own)
                if (a.Kind == ActorKind.Vehicle && view.IsPackedRig(a.Id) && a.Idle && view.PackProgressPermille(a.Ref) == 0) anyIdlePacked = true;
            idlePacked = anyIdlePacked ? idlePacked + 20 : 0;
            worst = Math.Max(worst, idlePacked);
        }
        log.WriteLine($"tier {tier} seed {seed}: end {sim.Tick}, defeated {sim.Players.Defeated[0]}/{sim.Players.Defeated[1]}, unpacks {lb.Unpacks}, raids {lb.RaidOrders}, salvage {lb.SalvageOrders}, reloc {lb.Relocations}/{lb.RelocationsDone}/{lb.RelocationsAborted}, storms {lb.StormOrders}, placed {lb.StructuresPlaced}, worst idle-packed {worst}");
        {
            var view = sim.ViewFor(0);
            var own = new List<Rts.Sim.View.ViewActor>();
            view.CopyOwn(own);
            foreach (var a in own)
                if (a.Kind == ActorKind.Vehicle && view.IsPackedRig(a.Id))
                    log.WriteLine($"  packed rig -> {(view.UnpackTypeOf(a.Id) >= 0 ? rules.Buildings[view.UnpackTypeOf(a.Id)].Id : "?")} at {a.Cell} idle {a.Idle} mission {a.Mission} hp {a.Health}");
        }
        Assert.True(worst < 600, "a packed rig never stands idle for 30 seconds");
        Assert.True(lb.Unpacks >= 1);
    }
}
