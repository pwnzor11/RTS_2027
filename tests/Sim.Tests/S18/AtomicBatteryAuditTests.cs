// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit, FAD-01: the Authority Atomic Battery on the shipped data. It charges, but its Special order is always refused
/// because it needs an own tunnel entrance within 8 cells of the target and no Authority building is an entrance.</summary>
public sealed class AtomicBatteryAuditTests
{
    private static (S18World W, EntityId Battery, EntityId Tank) Make()
    {
        var w = new S18World();
        w.Place(0, "auth_power", 10, 10);
        w.Place(0, "auth_power", 13, 10);
        w.Place(0, "auth_power", 16, 10);
        EntityId battery = w.Place(0, "auth_atomic_battery", 10, 14);
        w.Place(0, "auth_barracks", 14, 14);
        w.Place(0, "auth_factory", 18, 14);
        EntityId tank = w.Veh(1, "auth_tank", 40, 40);
        return (w, battery, tank);
    }

    [Fact]
    public void Audit_NoAuthorityBuildingIsATunnelEntrance_ButTheBatteryNeedsOneInRange()
    {
        // S18 audit: pins current behaviour of FAD-01; flip the assertion when fixed.
        SimRules r = S18World.RealRules;
        BuildingType battery = r.Buildings.Single(b => b.Id == "auth_atomic_battery");
        Assert.Equal(8, battery.SuperweaponEntranceRange);
        Assert.DoesNotContain(r.Buildings, b => b.Faction == Faction.Authority && b.Entrance != TunnelSize.None);
        // the other three superweapons: the Directorate one also needs an entrance (it has entrances), the two newer ones fire anywhere
        Assert.Equal(8, r.Buildings.Single(b => b.Id == "dir_sunrise").SuperweaponEntranceRange);
        Assert.Equal(0, r.Buildings.Single(b => b.Id == "lb_second_drift").SuperweaponEntranceRange);
        Assert.Equal(0, r.Buildings.Single(b => b.Id == "exp_salvo_array").SuperweaponEntranceRange);
    }

    [Fact]
    public void Audit_ChargedBattery_RefusesEveryTargetCell_AndTheSpecialOrderDoesNothing()
    {
        // S18 audit: pins current behaviour of FAD-01; flip the assertion when fixed.
        var (w, battery, tank) = Make();
        int total = w.Rules.Buildings[w.B("auth_atomic_battery")].SuperweaponChargeTicks;
        w.Run(5);
        w.Sim.Effects.BuildingCharge[battery.Index] = total;   // fully charged
        Assert.True(w.Sim.ViewFor(0).Hud.Superweapons.Single().Ready);   // the HUD says "ready"

        int size = w.Sim.Setup.Map.Width, allowed = 0;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                if (w.Sim.CanFireSuperweapon(0, battery, new CellCoord(x, y))) allowed++;
        Assert.Equal(0, allowed);   // not one cell of the map is a legal target

        int hp = w.VehHp(tank);
        w.Run(1, w.Cmd(0, CommandKind.Special, battery, default, new CellCoord(40, 40)));
        w.Run(20);
        Assert.Equal(total, w.Sim.Effects.BuildingCharge[battery.Index]);   // still charged: the shot never left
        Assert.Equal(hp, w.VehHp(tank));
    }

    [Fact(Skip = "S18 FAD-01: awaiting owner decision")]
    public void Desired_ChargedBattery_FiresAtAnEnemyAndResetsItsCharge()
    {
        var (w, battery, tank) = Make();
        w.Run(5);
        w.Sim.Effects.BuildingCharge[battery.Index] = w.Rules.Buildings[w.B("auth_atomic_battery")].SuperweaponChargeTicks;
        Assert.True(w.Sim.CanFireSuperweapon(0, battery, new CellCoord(40, 40)));
        int hp = w.VehHp(tank);
        w.Run(1, w.Cmd(0, CommandKind.Special, battery, default, new CellCoord(40, 40)));
        w.Run(20);
        Assert.True(w.Sim.Effects.BuildingCharge[battery.Index] < 100);
        Assert.True(w.VehHp(tank) < hp);
    }
}
