// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit on the shipped data. FAD-05: there is no attack-ground order, so a Decon Truck cannot clean contaminated ground
/// unless an enemy stands on it. FAD-19: <c>needs_power</c> only stops a building firing; an unpowered Searchlight still sees through
/// Blackout and reveals stealth, and an unpowered Broadcast Mast still jams.</summary>
public sealed class GroundEffectAuditTests
{
    private static readonly CellCoord Hot = new(23, 20);

    /// <summary>Radiation 200 on the 3x3 around <see cref="Hot"/>; a Decon Truck 3 cells away gets every order a player could give it for
    /// a ground cell; optionally an enemy Silo stands on the hot cell and the truck attacks it. Returns the radiation left on a
    /// neighbouring hot cell after 400 ticks.</summary>
    private static int RadiationLeft(bool truck, bool enemyOnIt)
    {
        var w = new S18World();
        for (int y = Hot.Y - 1; y <= Hot.Y + 1; y++)
            for (int x = Hot.X - 1; x <= Hot.X + 1; x++) w.Sim.Ground.Radiation[w.Sim.Ground.IndexOf(new CellCoord(x, y))] = 200;
        EntityId silo = enemyOnIt ? w.Place(1, "auth_silo", Hot.X, Hot.Y) : default;
        EntityId t = truck ? w.Veh(0, "auth_decon_truck", 20, 20) : default;
        if (truck && enemyOnIt) w.Run(1, w.Attack(0, ActorKind.Vehicle, t, ActorKind.Building, silo));
        else if (truck)
        {
            w.Run(1, w.Cmd(0, CommandKind.Attack, t, default, Hot, (int)ActorKind.Vehicle));          // attack with a cell and no target
            w.Run(1, w.Cmd(0, CommandKind.AttackMove, t, default, new CellCoord(21, 20), (int)ActorKind.Vehicle));
            w.Run(60);
            w.Run(1, w.Cmd(0, CommandKind.Guard, t, default, new CellCoord(21, 20), (int)ActorKind.Vehicle));
        }
        w.Run(400 - w.Sim.Tick);   // every variant runs the same 400 ticks, so natural decay is equal
        return w.Sim.Ground.RadiationAt(new CellCoord(Hot.X - 1, Hot.Y));
    }

    [Fact]
    public void Audit_DeconTruck_CannotCleanEmptyGround_OnlyGroundAnEnemyStandsOn()
    {
        // S18 audit: pins current behaviour of FAD-05; flip the assertion when fixed.
        Assert.DoesNotContain(Enum.GetNames<CommandKind>(), n => n.Contains("Ground", StringComparison.OrdinalIgnoreCase) || n == "AttackCell");
        int untouched = RadiationLeft(truck: false, enemyOnIt: false);
        int ordered = RadiationLeft(truck: true, enemyOnIt: false);
        int withEnemy = RadiationLeft(truck: true, enemyOnIt: true);
        Assert.True(untouched > 0);
        Assert.Equal(untouched, ordered);       // no order makes the truck spray the bare hot cell
        Assert.True(withEnemy < untouched);     // it sprays only as a side effect of attacking an enemy actor there
    }

    [Fact(Skip = "S18 FAD-05: awaiting owner decision")]
    public void Desired_DeconTruck_CleansGroundItIsOrderedTo()
    {
        Assert.True(RadiationLeft(truck: true, enemyOnIt: false) < RadiationLeft(truck: false, enemyOnIt: false));
    }

    [Fact]
    public void UnpoweredSearchlight_SeesNothingThroughBlackout_AndRevealsNoStealth_LikeAnUnpoweredTurret()
    {
        // S19 (FAD-19 fixed): needs_power now also stops the Searchlight's sight.
        var w = new S18World(keepAlive: false);
        w.Place(1, "auth_conyard", 10, 50);
        w.Place(0, "auth_searchlight", 20, 20);
        w.Place(0, "auth_turret", 20, 26);
        EntityId enemy = w.Inf(1, "auth_rifleman", 22, 26);
        Assert.True(w.Rules.Buildings[w.B("auth_searchlight")].NeedsPower);
        Assert.True(w.Rules.Buildings[w.B("auth_turret")].NeedsPower);
        w.Sim.Ground.Blackout[w.Sim.Ground.IndexOf(new CellCoord(25, 20))] = 5000;
        int max = w.InfHp(enemy);
        w.Run(100);

        Assert.True(w.Sim.PowerBand(0) >= 1);                                       // no power at all
        Assert.Equal(max, w.InfHp(enemy));                                           // the turret obeys needs_power
        Assert.False(w.Sim.CanSeeThroughBlackout(0, new CellCoord(25, 20)));         // nor does the Searchlight now
        Assert.False(w.Sim.IsStealthRevealed(0, new CellCoord(26, 20)));
        Assert.False(w.Sim.CanSeeThroughBlackout(0, new CellCoord(40, 40)));         // (outside its sight: not covered)
    }

    [Fact]
    public void UnpoweredBroadcastMast_NoLongerJams()
    {
        // S19 (FAD-19 fixed).
        var w = new S18World(Faction.Authority, Faction.Directorate, keepAlive: false);
        w.Place(0, "auth_conyard", 10, 10);
        w.Place(1, "dir_mast", 40, 40);
        Assert.True(w.Rules.Buildings[w.B("dir_mast")].NeedsPower);
        w.Run(20);
        Assert.True(w.Sim.PowerBand(1) >= 1);
        Assert.False(w.Sim.IsJammedFor(0, new CellCoord(44, 44)));
        Assert.False(w.Sim.IsJammedFor(0, new CellCoord(52, 52)));
    }

    [Fact]
    public void Desired_UnpoweredSearchlightAndMast_DoNothing()
    {
        var w = new S18World(Faction.Authority, Faction.Directorate, keepAlive: false);
        w.Place(0, "auth_searchlight", 20, 20);
        w.Place(1, "dir_mast", 40, 40);
        w.Sim.Ground.Blackout[w.Sim.Ground.IndexOf(new CellCoord(25, 20))] = 5000;
        w.Run(20);
        Assert.False(w.Sim.CanSeeThroughBlackout(0, new CellCoord(25, 20)));
        Assert.False(w.Sim.IsJammedFor(0, new CellCoord(44, 44)));
    }
}
