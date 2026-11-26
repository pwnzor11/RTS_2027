// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Production;
using Rts.Sim.Rules;
using Rts.Sim.Tests.S18;

namespace Rts.Sim.Tests.S44;

/// <summary>Session 44 rule fixes: M-20 (the irradiated tag is gone), M-21 (Mass Conscription speeds up conscripts), M-22 (flak spares
/// its own aircraft), M-23 (a cost drop mid-build refunds), M-24 (a rig refuses to unpack into a full building store), M-25 (ashfall
/// resistance scales).</summary>
public sealed class RuleFixesTests
{
    // ---- M-20 -------------------------------------------------------------------------------------

    [Fact]
    public void M20_NoIrradiatedTagOrBonusFieldRemains()
    {
        Assert.Null(typeof(VehicleType).GetProperty("Irradiated"));
        Assert.Null(typeof(InfantryType).GetProperty("Irradiated"));
        Assert.Null(typeof(WarheadType).GetProperty("BonusVsIrradiatedPercent"));
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "data", "rules", "game.toml"))) root = Path.GetDirectoryName(root)!;
        foreach (string f in new[] { "infantry.toml", "vehicles.toml", "warheads.toml" })
            Assert.DoesNotContain("irradiated", File.ReadAllText(Path.Combine(root!, "data", "rules", f)).Replace("irradiated runner", ""));
    }

    // ---- M-21 -------------------------------------------------------------------------------------

    [Fact]
    public void M21_MassConscription_ActsOnAnExplicitBuildTime()
    {
        var w = new S18World(Faction.Directorate, Faction.Authority);
        int c = w.I("dir_conscript");
        InfantryType before = w.Sim.RulesOf(0).Infantry![c];
        Assert.Equal(32, before.BuildTime);
        Assert.Equal(80, before.Cost);
        w.Run(1, w.Grant(0, "dir_up_mass_conscription"));
        InfantryType after = w.Sim.RulesOf(0).Infantry![c];
        Assert.Equal(64, after.Cost);
        Assert.Equal(25, after.BuildTime);   // 32 x 0.8: the effect now acts, and matches the old cost-derived 64 x 40 / 100
    }

    // ---- M-22 -------------------------------------------------------------------------------------

    [Fact]
    public void M22_FlakBurst_HurtsEnemyAircraft_ButSparesOwnAircraft()
    {
        var w = new S18World(Faction.Authority, Faction.Authority);
        EntityId enemy = w.Veh(1, "auth_interceptor", 30, 30);
        EntityId own = w.Veh(0, "auth_interceptor", 30, 31);
        int he = SimRules.IndexOf(w.Rules.Warheads, "auth_he", x => x.Id);
        Assert.True(w.Rules.Warheads[he].HitsAllies);   // the warhead still hurts allies for ground weapons
        int hp0 = w.VehHp(enemy), own0 = w.VehHp(own);
        w.Sim.Detonate(0, 20, he, LeptonCoord.CenterOf(new CellCoord(30, 30)), new ActorRef(ActorKind.Vehicle, enemy), false,
            default, WeaponTargets.Air);
        w.Run(1);
        Assert.True(w.VehHp(enemy) < hp0);
        Assert.Equal(own0, w.VehHp(own));
    }

    // ---- M-23 -------------------------------------------------------------------------------------

    [Fact]
    public void M23_CostDropLateInABuild_RefundsTheDifference()
    {
        var w = new S18World(Faction.Directorate, Faction.Authority);
        w.Place(0, "dir_shaft", 14, 14);   // supply cap
        w.Place(0, "dir_reactor_vent", 26, 20);
        w.Place(0, "dir_muster_hatch", 20, 20);
        w.Run(1, w.Cmd(0, CommandKind.Debug, default, default, default, (int)DebugOp.SetCash, 9000));
        w.Run(1, w.Cmd(0, CommandKind.Build, default, default, default, (int)ProductionCategory.Infantry, w.I("dir_conscript")));
        int lane = ProductionState.Lane(0, ProductionCategory.Infantry);
        for (int i = 0; i < 200 && w.Sim.Production.Ticks[lane] < 28; i++) w.Run(1);
        Assert.True(w.Sim.Production.Spent[lane] >= 65, $"unl={w.Sim.IsUnlocked(0, ProductionCategory.Infantry, w.I("dir_conscript"))} qb={w.Sim.QueueBlockReason(0, ProductionCategory.Infantry, w.I("dir_conscript"))} q={w.Sim.Production.QueueCount[lane]} t={w.Sim.Production.Ticks[lane]} tot={w.Sim.Production.Total[lane]} sp={w.Sim.Production.Spent[lane]} cash={w.Sim.Players.Cash[0]} st={w.Sim.Production.Stalled[lane]} tick={w.Sim.Tick}");
        Assert.InRange(w.Sim.Production.Spent[lane], 65, 80);   // already paid more than the coming 64
        w.Run(1, w.Grant(0, "dir_up_mass_conscription"));
        w.Run(80);
        Assert.Equal(0, w.Sim.Production.QueueCount[lane]);
        Assert.Equal(9000 - 64, w.Sim.Players.Cash[0]);
    }

    // ---- M-24 -------------------------------------------------------------------------------------

    [Fact]
    public void M24_UnpackIsRefusedWhileTheBuildingStoreIsFull()
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority, 128);
        EntityId rigV = w.Veh(0, "lb_mother_rig_packed", 20, 20);
        int packed = rigV.Index;
        w.Sim.Rigs.PackedFrom[packed] = w.B("lb_mother_rig") + 1;
        Assert.True(w.Sim.Rigs.IsPackedRig(packed));
        CellCoord site = w.Sim.Units.Position[packed].ToCell();
        int into = w.Sim.UnpackTypeOf(packed);
        Assert.True(w.Sim.UnpackSiteOk(0, into, site, packed));

        int wall = w.B("auth_wall");
        for (int y = 60; y < 120 && !w.Sim.BuildingStoreFull; y += 2)
            for (int x = 2; x < 126 && !w.Sim.BuildingStoreFull; x += 2) w.Sim.PlaceBuilding(1, wall, new CellCoord(x, y));
        Assert.True(w.Sim.BuildingStoreFull);
        Assert.False(w.Sim.UnpackSiteOk(0, into, site, packed));

        w.Run(1, w.Cmd(0, CommandKind.Pack, w.Sim.Units.Slots.IdOf(packed), default, site));
        Assert.Equal(0, w.Sim.Rigs.UnpackTicksLeft[packed]);
        w.Run(w.Rules.Buildings[w.B("lb_mother_rig")].PackTicks * 2);
        Assert.True(w.Sim.Units.Slots.IsIndexAlive(packed) && w.Sim.Rigs.IsPackedRig(packed));
    }

    // ---- M-25 -------------------------------------------------------------------------------------

    [Fact]
    public void M25_AshfallDamage_ScalesWithResistance()
    {
        var w = new S18World(Faction.Authority, Faction.Directorate);
        var field = new CellCoord(30, 30);
        for (int y = 26; y <= 34; y++)
            for (int x = 26; x <= 34; x++) w.Sim.Map.SetOverlay(new CellCoord(x, y), OverlayKind.Ashfall, 100);
        EntityId plain = w.Inf(0, "exp_marine", field.X, field.Y);
        EntityId res600 = w.Inf(1, "dir_conscript", field.X, field.Y, 1);
        w.Run(30 * 10);
        int lostPlain = w.Rules.Infantry![w.I("exp_marine")].MaxHealth - w.InfHp(plain);
        int lost600 = w.Rules.Infantry![w.I("dir_conscript")].MaxHealth - w.InfHp(res600);
        Assert.Equal(20, lostPlain);           // 10 hits x 2
        Assert.Equal(8, lost600);              // 20 x 400/1000: proportional, not a flat 1 per hit (which would be 10)
        // Lead Lining (rad resist 900) cuts it further
        var w2 = new S18World(Faction.Authority, Faction.Directorate);
        for (int y = 26; y <= 34; y++)
            for (int x = 26; x <= 34; x++) w2.Sim.Map.SetOverlay(new CellCoord(x, y), OverlayKind.Ashfall, 100);
        EntityId lined = w2.Inf(1, "dir_conscript", field.X, field.Y, 1);
        w2.Run(1, w2.Grant(1, "dir_up_lead_lining"));
        Assert.Equal(900, w2.Sim.RulesOf(1).Infantry![w2.I("dir_conscript")].RadResistPermille);
        w2.Run(30 * 10);
        int lost900 = w2.Rules.Infantry![w2.I("dir_conscript")].MaxHealth - w2.InfHp(lined);
        Assert.InRange(lost900, 1, 3);   // about 2 of 20; a flat 1 per hit would be 10
    }
}
