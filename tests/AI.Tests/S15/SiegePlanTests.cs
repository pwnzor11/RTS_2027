// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S15;

/// <summary>S15 (Q-64, D-174): the pure siege decisions (defence estimate, army need by tier, target priority, artillery test).</summary>
public sealed class SiegePlanTests
{
    private static SimRules Rules => TestWorld.Rules.Sim;
    private static int B(string id) => SimRules.IndexOf(Rules.Buildings, id, b => b.Id);
    private static int V(string id) => SimRules.IndexOf(Rules.Vehicles, id, v => v.Id);

    private static ViewActor Bld(int slot, string id, int x, int y, bool ghost = false) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Building, 1, Relation.Enemy, B(id), new CellCoord(x, y), new LeptonCoord(x * 256, y * 256),
            100, 100, MissionKind.Guard, false, EntityId.None, ghost, 1, 1);

    private static ViewActor Veh(int slot, string id) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Vehicle, 0, Relation.Self, V(id), new CellCoord(5, 5), new LeptonCoord(5 * 256, 5 * 256),
            100, 100, MissionKind.Guard, true, EntityId.None, false, 1, 1);

    [Fact]
    public void OnlyTiersTwoAndUpSiege()
    {
        Assert.False(SiegePlan.Enabled(1));
        for (int t = 2; t <= 5; t++) Assert.True(SiegePlan.Enabled(t));
        Assert.Equal(0, SiegePlan.Need(1, 5000));
        Assert.Equal(0, SiegePlan.MinUnits(1, 3));
    }

    [Fact]
    public void HigherTiersGatherMoreForTheSameDefence()
    {
        int prev = 0;
        for (int t = 2; t <= 5; t++)
        {
            int need = SiegePlan.Need(t, 2000);
            Assert.True(need > prev, $"tier {t}");
            prev = need;
        }
        Assert.True(SiegePlan.MinUnits(5, 0) > SiegePlan.MinUnits(2, 0));
        Assert.Equal(SiegePlan.NeedCap, SiegePlan.Need(5, 1_000_000));
        Assert.Equal(SiegePlan.MinUnits(3, SiegePlan.MaxFailureSteps), SiegePlan.MinUnits(3, 99));
    }

    [Fact]
    public void DefenceValueCountsArmedGroundStructuresOnly()
    {
        var known = new List<ViewActor>
        {
            Bld(1, "dir_pillbox", 10, 10), Bld(2, "dir_rad_mortar", 12, 10),
            Bld(3, "dir_rocket_nest", 14, 10),   // air only: no threat to a ground army
            Bld(4, "dir_smelter_vent", 16, 10),
        };
        int value = SiegePlan.DefenceValue(Rules, known, out int range);
        Assert.Equal(Rules.Buildings[B("dir_pillbox")].Cost + Rules.Buildings[B("dir_rad_mortar")].Cost, value);
        Assert.Equal(9, range);   // the mortar
    }

    [Fact]
    public void ArtilleryIsLongRangeGroundFire()
    {
        Assert.True(SiegePlan.IsArtillery(Rules, Veh(1, "auth_howitzer")));
        Assert.True(SiegePlan.IsArtillery(Rules, Veh(2, "dir_rocket_truck")));
        Assert.False(SiegePlan.IsArtillery(Rules, Veh(3, "auth_tank")));
        Assert.False(SiegePlan.IsArtillery(Rules, Veh(4, "auth_interceptor")));   // air-to-air, short
    }

    [Fact]
    public void AnUncoveredProductionStructureBeatsAFarDefenceButNotACoveringOne()
    {
        // army at 5,5: a pillbox 4 cells away, a factory-type structure 12 cells away with nothing around it
        var open = new List<ViewActor> { Bld(1, "dir_pillbox", 9, 5), Bld(2, "dir_assembly_bore", 17, 5) };
        ViewActor? pick = SiegePlan.PickTarget(Rules, open, new CellCoord(5, 5), _ => false);
        Assert.Equal(open[0].Id, pick?.Id);   // 16 vs 48 - 28 = 20: the near defence still first
        var far = new List<ViewActor> { Bld(1, "dir_pillbox", 9, 5), Bld(2, "dir_assembly_bore", 12, 5) };
        // uncovered would be 28 - 28 = 0 < 16: the producer goes first; a pillbox right beside it covers it, so the defence goes first
        pick = SiegePlan.PickTarget(Rules, far, new CellCoord(5, 5), _ => false);
        Assert.Equal(far[0].Id, pick?.Id);   // pillbox (range 3) at 9,5 covers 12,5: not in reach yet
        far[0] = Bld(1, "dir_pillbox", 9, 0);   // moved away: 12,5 is no longer covered
        pick = SiegePlan.PickTarget(Rules, far, new CellCoord(5, 5), _ => false);
        Assert.Equal(far[1].Id, pick?.Id);
    }

    [Fact]
    public void AvoidedCellsAreSkippedAndWallsNever()
    {
        var known = new List<ViewActor> { Bld(1, "dir_pillbox", 9, 5), Bld(2, "dir_assembly_bore", 30, 30) };
        ViewActor? pick = SiegePlan.PickTarget(Rules, known, new CellCoord(5, 5), c => c == known[0].Cell);
        Assert.Equal(known[1].Id, pick?.Id);
        Assert.Null(SiegePlan.PickTarget(Rules, new List<ViewActor>(), new CellCoord(5, 5), _ => false));
    }
}
