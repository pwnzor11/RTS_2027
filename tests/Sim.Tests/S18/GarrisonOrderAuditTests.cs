// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using static Rts.Sim.Tests.M4.GarrisonFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIMDET-01 / SIM-RULES-01): ordinary unit orders sent to a soldier inside a garrison.</summary>
public sealed class GarrisonOrderAuditTests
{
    private static readonly CellCoord BunkerCell = new(10, 10);
    private static readonly CellCoord Far = new(20, 10);
    private const int Inf = (int)ActorKind.Infantry;

    private static (Simulation Sim, EntityId Bunker, EntityId Man) Garrisoned(Action<Simulation>? alsoSpawn = null, int type = Rifle)
    {
        Simulation sim = NewSim();
        alsoSpawn?.Invoke(sim);   // spawning is a setup-phase call
        EntityId bunker = sim.PlaceBuilding(0, Bunker, BunkerCell);
        EntityId man = sim.SpawnInfantry(0, type, new CellCoord(12, 10));
        Run(sim, 1, EnterCmd(sim, 0, man, bunker));
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
        Assert.Equal(MissionKind.Garrisoned, sim.Infantry.Mission[man.Index]);
        return (sim, bunker, man);
    }

    private static Command Order(Simulation sim, CommandKind kind, EntityId man, EntityId target = default, int targetKind = 0) => kind switch
    {
        CommandKind.Attack => Cmd(sim, 0, kind, man, target, default, Inf | (targetKind << 8)),
        _ => Cmd(sim, 0, kind, man, default, Far, Inf),
    };

    /// <summary>Guard on its own cell: spawned units have no mission and do not look for targets until ordered.</summary>
    private static Command GuardHere(Simulation sim, EntityId who, ActorKind kind) =>
        Cmd(sim, 1, CommandKind.Guard, who, default, kind == ActorKind.Vehicle ? sim.Units.Position[who.Index].ToCell() : sim.Infantry.Position[who.Index].ToCell(), (int)kind);

    // S18 audit: pins current behaviour of SIMDET-01 / SIM-RULES-01; flip the assertion when fixed.
    [Fact]
    public void Audit_MoveOrder_WalksAGarrisonedSoldierOut_WhileItsLayerStaysGarrisoned()
    {
        EntityId spotter = default;
        var (sim, bunker, man) = Garrisoned(s => spotter = s.SpawnVehicle(1, Dummy, new CellCoord(22, 12)));   // an enemy that sees the destination
        Run(sim, 1, Order(sim, CommandKind.Move, man));
        Assert.NotEqual(MissionKind.Garrisoned, sim.Infantry.Mission[man.Index]);   // the order replaced the garrison mission
        Run(sim, 400);
        CellCoord at = sim.Infantry.Position[man.Index].ToCell();
        Assert.True(Reach(at, BunkerCell, 2, 2) >= 5, $"the soldier should have walked away from the bunker, is at {at}");
        // ... but the sim still files it as inside:
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
        Assert.Equal(bunker, sim.Tunnels.InfantryCarrier[man.Index]);
        Assert.Equal(1, sim.OccupantsIn(bunker));
        Assert.True(sim.IsVisibleTo(1, at), "the enemy has vision of the cell the soldier stands on");
        Assert.True(sim.IsHiddenFrom(1, ActorKind.Infantry, man.Index));
        Assert.True(sim.IsUntargetableBy(1, ActorKind.Infantry, man.Index));
        Assert.DoesNotContain(sim.Snapshot(1).Units, u => u.Kind == UnitKind.Infantry && u.Id == man);
        Assert.True(sim.Units.Slots.IsAlive(spotter));
    }

    // S18 audit: pins current behaviour of SIMDET-01 / SIM-RULES-01; flip the assertion when fixed.
    [Theory]
    [InlineData(CommandKind.Stop)]
    [InlineData(CommandKind.Guard)]
    [InlineData(CommandKind.Scatter)]
    [InlineData(CommandKind.AttackMove)]
    [InlineData(CommandKind.Attack)]
    public void Audit_OtherUnitOrders_AlsoReplaceTheGarrisonedMission(CommandKind kind)
    {
        EntityId foe = default;
        var (sim, _, man) = Garrisoned(s => foe = s.SpawnVehicle(1, Dummy, new CellCoord(17, 10)));
        Run(sim, 2, Order(sim, kind, man, foe, (int)ActorKind.Vehicle));
        Assert.NotEqual(MissionKind.Garrisoned, sim.Infantry.Mission[man.Index]);
        Assert.Equal(ActorLayer.Garrisoned, sim.LayerOf(ActorKind.Infantry, man.Index));
    }

    // S18 audit: pins current behaviour of SIMDET-01 / SIM-RULES-01; flip the assertion when fixed.
    [Fact]
    public void Audit_WalkedOutSoldier_Fires_IsNotShotBack_AndIsSplashImmune()
    {
        EntityId victim = default, foeRifle = default, mortar = default;
        var (sim, bunker, man) = Garrisoned(s =>
        {
            victim = s.SpawnVehicle(1, Dummy, new CellCoord(Far.X + 3, Far.Y));       // all out of reach of the bunker (range 6 + 1)
            foeRifle = s.SpawnInfantry(1, Rifle, new CellCoord(Far.X + 2, Far.Y + 1));
            mortar = s.SpawnVehicle(1, Mortar, new CellCoord(Far.X + 4, Far.Y + 2));
        });
        Run(sim, 600, Order(sim, CommandKind.Move, man), GuardHere(sim, foeRifle, ActorKind.Infantry), GuardHere(sim, mortar, ActorKind.Vehicle));
        Assert.Equal(Far, sim.Infantry.Position[man.Index].ToCell());   // walked to (20,10), then stood there on Guard for some 20 s
        Assert.Equal(400, sim.Buildings.Health[bunker.Index]);         // nobody could reach the bunker: all fire below is at/from the walker
        Assert.True(sim.Infantry.Slots.IsAlive(man));
        Assert.Equal(50, sim.Infantry.Health[man.Index]);              // two armed enemies in range never touched it
        Assert.True(sim.Units.Health[victim.Index] < 1000 || sim.Infantry.Health[foeRifle.Index] < 50 || !sim.Infantry.Slots.IsAlive(foeRifle)
            || sim.Units.Health[mortar.Index] < 1000, "the walked-out soldier should have fired at an enemy");
    }

    /// <summary>Control for the test above: the same walk by a soldier that never garrisoned ends with it shot at.</summary>
    [Fact]
    public void Control_SurfaceSoldierAtTheSameSpot_IsShot()
    {
        Simulation sim = NewSim();
        sim.PlaceBuilding(0, Bunker, BunkerCell);
        EntityId man = sim.SpawnInfantry(0, Rifle, new CellCoord(12, 10));
        sim.SpawnVehicle(1, Dummy, new CellCoord(Far.X + 3, Far.Y));
        EntityId foeRifle = sim.SpawnInfantry(1, Rifle, new CellCoord(Far.X + 2, Far.Y + 1));
        EntityId mortar = sim.SpawnVehicle(1, Mortar, new CellCoord(Far.X + 4, Far.Y + 2));
        Run(sim, 600, Order(sim, CommandKind.Move, man), GuardHere(sim, foeRifle, ActorKind.Infantry), GuardHere(sim, mortar, ActorKind.Vehicle));
        Assert.True(!sim.Infantry.Slots.IsAlive(man) || sim.Infantry.Health[man.Index] < 50);
    }

    [Theory(Skip = "S18 SIMDET-01 / SIM-RULES-01: awaiting owner decision")]
    [InlineData(CommandKind.Move)]
    [InlineData(CommandKind.Stop)]
    [InlineData(CommandKind.Guard)]
    [InlineData(CommandKind.Scatter)]
    [InlineData(CommandKind.AttackMove)]
    [InlineData(CommandKind.Attack)]
    public void Desired_UnitOrders_AreIgnoredByAGarrisonedSoldier(CommandKind kind)
    {
        EntityId foe = default;
        var (sim, bunker, man) = Garrisoned(s => foe = s.SpawnVehicle(1, Dummy, new CellCoord(17, 10)));
        Run(sim, 200, Order(sim, kind, man, foe, (int)ActorKind.Vehicle));
        // Either the order is dropped (still inside, mission Garrisoned, at the centre) or the soldier is properly ejected (layer Surface).
        bool inside = sim.LayerOf(ActorKind.Infantry, man.Index) == ActorLayer.Garrisoned;
        if (inside)
        {
            Assert.Equal(MissionKind.Garrisoned, sim.Infantry.Mission[man.Index]);
            Assert.Equal(new LeptonCoord(11 * 256, 11 * 256), sim.Infantry.Position[man.Index]);
            Assert.Equal(1, sim.OccupantsIn(bunker));
        }
        else
        {
            Assert.Equal(ActorLayer.Surface, sim.LayerOf(ActorKind.Infantry, man.Index));
            Assert.Equal(0, sim.OccupantsIn(bunker));
            Assert.False(sim.IsHiddenFrom(1, ActorKind.Infantry, man.Index) && sim.IsVisibleTo(1, sim.Infantry.Position[man.Index].ToCell()));
        }
    }
}
