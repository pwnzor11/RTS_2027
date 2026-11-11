// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using AI.Tests.Tiers;
using Rts.AI;
using Rts.AI.Recruit;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit.Abstractions;

namespace AI.Tests.S18;

/// <summary>S18 audit: the AI stalls of the wave 1 AI review: AI-04 (an unplaceable ready structure blocks the structure lane), AI-05
/// (tiers 4-5 build no vehicles while an aircraft is in production), AI-06 (a Bore is deployed once and never re-ordered) and AI-09
/// (RepairManager toggles repair off when health keeps falling).</summary>
public class AiStallAuditTests(ITestOutputHelper output)
{
    private static SimRules Rules => TestWorld.Rules.Sim;

    private sealed class Log : ICommandSink
    {
        public readonly List<(CommandKind Kind, EntityId Subject, CellCoord Cell)> Orders = new();
        public void Issue(CommandKind kind, EntityId subject, ActorKind subjectKind, CellCoord cell = default, ActorRef target = default, int arg2 = 0)
            => Orders.Add((kind, subject, cell));
        public void Build(ProductionCategory category, int typeIndex) { }
        public void Place(int buildingType, CellCoord topLeft) { }
        public void Rally(EntityId factory, CellCoord cell) { }
    }

    private static QueueView Lane(Simulation sim, byte p, ProductionCategory cat) => sim.HudFor(p).Queues.First(q => q.Category == cat);

    // ---- AI-04 ------------------------------------------------------------------------------------

    // S18 audit: pins current behaviour of AI-04; flip the assertion when fixed.
    // Glass Coast layout 1, size 96, seed 4, Expedition tier 3 mirror, game 1 of the pair as `ai match --starts slot` plays it.
    [Fact]
    public void Audit_GlassCoastSeed4_ExpeditionAi_HoldsAnUnplaceableReadyStructureForTheWholeGame()
    {
        const ulong seed = 4;
        var gen = MapGenerator.GenerateWithMeta(new MapGenParams(Biome.GlassCoast, MapSize.Medium, 2, seed, null, OceanEdges.North | OceanEdges.South, 1));
        var side = new AiSide(AiRegistry.TieredId, TestWorld.Tiers[2], Faction.Expedition);
        var readySince = new[] { -1, -1 };
        var readyType = new[] { -1, -1 };
        var ais = new RecruitAi?[2];
        Simulation? last = null;
        AiMatchRunner.Play(gen.Map, Rules, TestWorld.Rules.RulesHash, seed * 16, side, side, true, 4000, observe: (sim, a, b, sa, sb) =>
        {
            last = sim;
            ais[sa] = a as RecruitAi; ais[sb] = b as RecruitAi;
            for (byte p = 0; p < 2; p++)
            {
                int r = Lane(sim, p, ProductionCategory.Structure).ReadyTypeIndex;
                if (r != readyType[p]) { readyType[p] = r; readySince[p] = sim.Tick; }
            }
        }, landing: gen.Meta.Landing);
        Simulation s = last!;
        for (byte p = 0; p < 2; p++)
        {
            int buildings = 0;
            for (int i = 1; i < s.Buildings.Capacity; i++) if (s.Buildings.Slots.IsIndexAlive(i) && s.Buildings.Player[i] == p) buildings++;
            string ready = readyType[p] >= 0 ? Rules.Buildings[readyType[p]].Id : "none";
            output.WriteLine($"p{p}: buildings {buildings}, ready structure {ready} since tick {readySince[p]}, placed {ais[p]!.StructuresPlaced}, unplaceable {ais[p]!.Unplaceable}, cash {s.Funds(p)}");
            Assert.True(readyType[p] >= 0, $"p{p} has a finished structure waiting");
            Assert.True(s.Tick - readySince[p] >= 2000, $"p{p} has held it for at least 2 000 ticks");
            Assert.True(buildings <= 2, $"p{p} stopped at {buildings} buildings");
            // the counter only counts a site the search found that CanPlace then refused; here the search finds no site at all
            Assert.Equal(0, ais[p]!.Unplaceable);
        }
    }

    // ---- AI-05 ------------------------------------------------------------------------------------

    // S18 audit: pins current behaviour of AI-05; flip the assertion when fixed.
    // Measured: with cap 2 the block lasts while the first aircraft is built (no aircraft alive, one queued: "have" 1 < cap 2, lane busy).
    // Once one flies and one is queued, have = cap and the vehicle lane runs again. So the vehicle lane idles for one aircraft build
    // time whenever the AI has no aircraft left, not during every aircraft build.
    [Fact]
    public void Audit_Tier4_BuildsNoVehicleWhileItsFirstAircraftIsInProduction()
    {
        var rig = new TierRig(Faction.Directorate, TestWorld.Tiers[3] with { ActiveScouting = false, Harass = false }, r =>
        {
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_smelter_vent", yard);
            r.PlaceNear(0, "dir_assembly_bore", yard);
            r.PlaceNear(0, "dir_isotope_works", yard);
            r.PlaceNear(0, "dir_mooring", yard);
        });
        int lastVehType = -1, emptySince = 0, longestIdleWithAir = 0, longestIdleWithoutAir = 0;
        bool lastAir = false, airDuringIdle = false;
        var events = new List<string>();
        for (int t = 0; t < 5000; t++)
        {
            if (rig.Sim.Players.Cash[0] < 30000) rig.Sim.Players.Cash[0] = 30000;   // money is never the reason
            rig.Run(1);
            bool air = Lane(rig.Sim, 0, ProductionCategory.Aircraft).Items.Count > 0;
            var vq = Lane(rig.Sim, 0, ProductionCategory.Vehicle).Items;
            int vt = vq.Count > 0 ? vq[0].TypeIndex : -1;
            if (vt != lastVehType) events.Add($"t{rig.Sim.Tick} vehicle lane {(vt < 0 ? "empty" : Rules.Vehicles[vt].Id)}");
            if (air != lastAir) events.Add($"t{rig.Sim.Tick} aircraft lane {(air ? "busy" : "empty")}");
            if (vt < 0 && lastVehType >= 0) { emptySince = rig.Sim.Tick; airDuringIdle = false; }
            if (vt < 0 && air) airDuringIdle = true;
            if (vt >= 0 && lastVehType < 0 && rig.Sim.Tick > 2)
            {
                int idle = rig.Sim.Tick - emptySince;
                if (airDuringIdle) longestIdleWithAir = Math.Max(longestIdleWithAir, idle);
                else longestIdleWithoutAir = Math.Max(longestIdleWithoutAir, idle);
            }
            lastVehType = vt; lastAir = air;
        }
        output.WriteLine($"aircraft ordered {rig.Ai.AircraftOrdered}; longest vehicle-lane idle with an aircraft in production {longestIdleWithAir} ticks, without {longestIdleWithoutAir} ticks");
        output.WriteLine(string.Join("\n", events));
        Assert.True(rig.Ai.AircraftOrdered >= 1);
        Assert.True(longestIdleWithoutAir <= 40, "control: otherwise the next vehicle starts about one decision after the last one");
        Assert.True(longestIdleWithAir >= 200, "the vehicle lane idled for the first aircraft's whole build");
    }

    // ---- AI-06 ------------------------------------------------------------------------------------

    // S18 audit: pins current behaviour of AI-06; flip the assertion when fixed.
    // A Bore whose dig failed surfaces as an idle unit (Simulation.BurrowMove BurrowArrived). The tactics give each Bore one Deploy order
    // for its lifetime and buy no new Bore while one exists, so the idle Bore is never used again.
    [Fact]
    public void Audit_IdleBore_IsDeployedOnce_AndNeverReordered_NorReplaced()
    {
        int bore = -1;
        for (int i = 0; i < Rules.Vehicles.Count; i++)
            if (Rules.Vehicles[i].Faction == Faction.Directorate && Rules.Vehicles[i].Locomotion == Locomotion.Burrow && Rules.Vehicles[i].DeploysInto >= 0) { bore = i; break; }
        Assert.True(bore >= 0);
        EntityId boreId = EntityId.None;
        var rig = new TierRig(Faction.Directorate, TestWorld.Tiers[2], r =>
        {
            CellCoord yard = r.Yard(0);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_reactor_vent", yard);
            r.PlaceNear(0, "dir_smelter_vent", yard);
            r.PlaceNear(0, "dir_assembly_bore", yard);
            r.Sim.Players.Cash[0] = 20000;
            boreId = r.Sim.SpawnVehicle(0, bore, new CellCoord(yard.X + 6, yard.Y + 6));
        });
        Assert.False(boreId.IsNone);
        var ctx = new AiContext
        {
            Player = 0, PlayerCount = 2, GameSeed = 1, Profile = TestWorld.Tiers[2], Rng = new AiRandom(1, 0),
            MapWidth = rig.Sim.Setup.Map.Width, MapHeight = rig.Sim.Setup.Map.Height, StartCell = rig.Sim.Setup.Map.StartPositions[0],
        };
        RecruitKnowledge Know(bool withBore)
        {
            var k = new RecruitKnowledge();
            k.Refresh(rig.Sim.ViewFor(0), "dir_shaft");
            if (!withBore) k.Bores.Clear();
            ViewActor army = k.Own.First(a => a.Kind == ActorKind.Infantry);
            for (int i = 0; i < 4; i++) k.Army.Add(army);
            if (k.Harvesters.Count == 0) k.Harvesters.Add(army);
            return k;
        }
        // control: with no Bore alive the tactics would buy one
        var control = new DirectorateTactics();
        control.Start(ctx);
        Assert.Equal(bore, control.WantedVehicle(Know(withBore: false)));

        var tactics = new DirectorateTactics();
        tactics.Start(ctx);
        var sink = new Log();
        CellCoord goal = rig.Sim.Setup.Map.StartPositions[1];
        RecruitKnowledge k1 = Know(withBore: true);
        Assert.Single(k1.Bores);
        Assert.True(k1.Bores[0].Idle);
        tactics.Think(k1, sink, goal, k1.Yard, null);
        // the same Bore, idle again (as after a failed dig), on later decisions
        for (int d = 0; d < 5; d++) tactics.Think(Know(withBore: true), sink, goal, k1.Yard, null);
        output.WriteLine($"deploy orders: {sink.Orders.Count(o => o.Kind == CommandKind.Deploy)}");
        Assert.Equal(1, sink.Orders.Count(o => o.Kind == CommandKind.Deploy && o.Subject == boreId));
        Assert.Equal(-1, tactics.WantedVehicle(Know(withBore: true)));
    }

    // ---- AI-09 ------------------------------------------------------------------------------------

    // S18 audit: pins current behaviour of AI-09; flip the assertion when fixed.
    // The sim's Repair command toggles (Simulation.Production ApplyRepair), so a second Repair order switches repair OFF.
    [Fact]
    public void Audit_RepairManager_TogglesRepairAgain_WhileHealthKeepsFalling()
    {
        int power = SimRules.IndexOf(Rules.Buildings, "auth_power", b => b.Id);
        var id = new EntityId(7, 1);
        ViewActor At(int health) => new(id, ActorKind.Building, 0, Relation.Self, power, new CellCoord(10, 10), new LeptonCoord(10 * 256, 10 * 256),
            health, 1000, MissionKind.None, true, EntityId.None, false, 2, 2);
        var repair = new RepairManager();
        var sink = new Log();
        var orderAt = new List<int>();
        int health = 700;
        for (int decision = 0; decision < 10; decision++, health -= 30)   // under fire: loses more than it repairs every decision
        {
            var k = new RecruitKnowledge { Rules = Rules };
            k.Buildings.Add(At(health));
            int before = sink.Orders.Count;
            repair.Think(k, sink);
            if (sink.Orders.Count > before) orderAt.Add(decision);
        }
        output.WriteLine($"Repair orders at decisions {string.Join(", ", orderAt)}");
        Assert.Equal(new[] { 0, 4, 8 }, orderAt);   // on, off after 4 decisions, on again after 4 more
    }

    [Fact(Skip = "S18 AI-09: awaiting owner decision")]
    public void RepairManager_KeepsRepairOn_WhileHealthKeepsFalling()
    {
        int power = SimRules.IndexOf(Rules.Buildings, "auth_power", b => b.Id);
        var repair = new RepairManager();
        var sink = new Log();
        for (int d = 0, health = 700; d < 10; d++, health -= 30)
        {
            var k = new RecruitKnowledge { Rules = Rules };
            k.Buildings.Add(new ViewActor(new EntityId(7, 1), ActorKind.Building, 0, Relation.Self, power, new CellCoord(10, 10), new LeptonCoord(2560, 2560),
                health, 1000, MissionKind.None, true, EntityId.None, false, 2, 2));
            repair.Think(k, sink);
        }
        Assert.Single(sink.Orders);
    }
}
