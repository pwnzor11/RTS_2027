// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FACTION-LE-13: five upgrades whose real effect on the shipped data is smaller than their text. One test each,
/// showing what the upgrade actually changes.</summary>
public sealed class UpgradeTextAuditTests
{
    // ---- Salvage Rights: "wreck value x1.5" -----------------------------------------------------------------

    private static (int Wreck, int DrainPermille) SalvageRights(bool researched)
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        EntityId tank = w.Veh(1, "auth_tank", 30, 30);
        if (researched) w.Run(1, w.Grant(0, "lb_up_salvage_rights"), w.Grant(1, "lb_up_salvage_rights"));
        w.Run(2);
        w.Run(1, w.Cmd(1, CommandKind.Debug, tank, default, default, (int)DebugOp.Kill, (int)ActorKind.Vehicle));
        w.Run(2);
        int slot = w.Sim.FindWreckAt(new CellCoord(30, 30));
        Assert.True(slot > 0);
        return (w.Sim.Salvage.ValueStart[slot], w.Sim.UpgradePermille(0, u => u.SalvagePermille));
    }

    [Fact]
    public void Audit_SalvageRights_SpeedsTheDrain_ButAWreckIsWorthTheSame()
    {
        // S18 audit: pins current behaviour of FACTION-LE-13 (Salvage Rights); flip the assertion when fixed.
        var (plain, drainPlain) = SalvageRights(false);
        var (upgraded, drainUpgraded) = SalvageRights(true);
        Assert.Equal(200, plain);          // 25 % of an 800-credit tank
        Assert.Equal(plain, upgraded);     // the wreck's value does not change
        Assert.True(drainUpgraded > drainPlain, $"drain {drainPlain} -> {drainUpgraded}");   // only the credits per salvage visit do
    }

    // ---- Grapple: "plunder x1.5" ------------------------------------------------------------------------------

    private static int Plunder(int victimStored, bool researched)
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        EntityId refinery = w.Place(1, "auth_refinery", 30, 30);
        EntityId runner = w.Inf(0, "lb_hook_runner", 29, 31);
        if (researched) w.Sim.Players.Upgrades[0] |= 1UL << w.U("lb_up_grapple");
        w.Sim.Players.Stored[1] = victimStored;
        int before = w.Sim.Players.Cash[0];
        Assert.True(w.Sim.PlunderNow(ActorKind.Infantry, runner.Index, refinery));
        return w.Sim.Players.Cash[0] - before;
    }

    [Fact]
    public void Audit_Grapple_GainsNothingOnceTheVictimStores5000_BecauseTheCapComesAfterTheBonus()
    {
        // S18 audit: pins current behaviour of FACTION-LE-13 (Grapple); flip the assertion when fixed.
        Assert.Equal(1000, S18World.RealRules.Economy.PlunderCap);
        Assert.Equal(400, Plunder(2000, false));    // 20 %
        Assert.Equal(600, Plunder(2000, true));     // x1.5: the upgrade works on a poor victim
        Assert.Equal(1000, Plunder(5000, false));   // 20 % = the cap
        Assert.Equal(1000, Plunder(5000, true));    // capped: nothing gained
        Assert.Equal(1000, Plunder(20000, true));
    }

    // ---- Sonar Nets -------------------------------------------------------------------------------------------

    [Fact]
    public void Audit_SonarNets_RaisesPicketSight_ButNotItsSonarRadius()
    {
        // S18 audit: pins current behaviour of FACTION-LE-13 (Sonar Nets); flip the assertion when fixed.
        var w = new S18World(Faction.Expedition, Faction.Authority, waterFromX: 40, keepAlive: false);
        w.Place(1, "auth_conyard", 10, 50);
        w.Place(0, "exp_picket", 38, 30);
        int picket = w.B("exp_picket");
        Assert.Equal(9, w.Sim.RulesOf(0).Buildings[picket].Sight);
        Assert.Equal(9, w.Sim.RulesOf(0).Buildings[picket].SonarRadius);
        w.Run(1, w.Grant(0, "exp_up_sonar_nets"));
        w.Run(1);
        Assert.Equal(11, w.Sim.RulesOf(0).Buildings[picket].Sight);
        Assert.Equal(9, w.Sim.RulesOf(0).Buildings[picket].SonarRadius);
        Assert.True(w.Sim.IsSonarRevealed(0, new CellCoord(47, 30)));    // 9 cells out: covered before and after
        Assert.False(w.Sim.IsSonarRevealed(0, new CellCoord(48, 30)));   // 10 cells out: still not covered with the upgrade
    }

    // ---- Scrap Plate: "armour" --------------------------------------------------------------------------------

    [Fact]
    public void Audit_ScrapPlate_Adds20PercentHealth_NoArmourChange_AndSkipsTheAircraft()
    {
        // S18 audit: pins current behaviour of FACTION-LE-13 (Scrap Plate); flip the assertion when fixed.
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        SimRules before = w.Sim.RulesOf(0);
        w.Run(1, w.Grant(0, "lb_up_scrap_plate"));
        w.Run(1);
        SimRules after = w.Sim.RulesOf(0);
        foreach (string id in new[] { "lb_gleaner", "lb_hauler", "lb_buggy", "lb_harpoon_wagon", "lb_organ", "lb_tow", "lb_dust_sail", "lb_rolling_fort", "lb_mother_rig_packed" })
        {
            Assert.Equal(before.Vehicles[w.V(id)].MaxHealth * 12 / 10, after.Vehicles[w.V(id)].MaxHealth);
            Assert.Equal(before.Vehicles[w.V(id)].Armor, after.Vehicles[w.V(id)].Armor);
        }
        foreach (string id in new[] { "lb_rotor", "lb_kite" })
            Assert.Equal(before.Vehicles[w.V(id)].MaxHealth, after.Vehicles[w.V(id)].MaxHealth);
        Assert.Equal(1200, after.Vehicles[w.V("lb_mother_rig_packed")].MaxHealth);   // every packed rig, whatever it carries
    }

    // ---- Patchwork: "Tinkers repair vehicles at range" --------------------------------------------------------------

    /// <summary>A Tinker with damaged Buggies 3, 5 and 6 cells away; returns the health each gained in 60 ticks.</summary>
    private static int[] TinkerHeals(bool researched)
    {
        var w = new S18World(Faction.LeftBehind, Faction.Authority);
        w.Inf(0, "lb_tinker", 20, 20);
        EntityId[] buggies = { w.Veh(0, "lb_buggy", 23, 20), w.Veh(0, "lb_buggy", 25, 20), w.Veh(0, "lb_buggy", 26, 20) };
        foreach (EntityId b in buggies) w.Sim.Units.Health[b.Index] = 50;
        if (researched) w.Run(1, w.Grant(0, "lb_up_patchwork")); else w.Run(1);
        w.Run(60);
        return buggies.Select(b => w.VehHp(b) - 50).ToArray();
    }

    [Fact]
    public void Audit_Patchwork_OnlyAddsTwoCells_TinkersAlreadyRepairAtThreeWithoutIt()
    {
        // S18 audit: pins current behaviour of FACTION-LE-13 (Patchwork); flip the assertion when fixed.
        int[] plain = TinkerHeals(false), upgraded = TinkerHeals(true);
        Assert.True(plain[0] > 0, "3 cells: repaired without the upgrade");
        Assert.Equal(0, plain[1]);
        Assert.Equal(0, plain[2]);
        Assert.True(upgraded[0] > 0);
        Assert.True(upgraded[1] > 0, "5 cells: repaired with the upgrade");
        Assert.Equal(0, upgraded[2]);
        Assert.Equal(plain[0], upgraded[0]);   // no faster, only further
    }
}
