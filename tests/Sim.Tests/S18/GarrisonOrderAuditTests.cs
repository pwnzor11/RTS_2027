// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using static Rts.Sim.Tests.M4.GarrisonFixture;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIMDET-01 / SIM-RULES-01), fixed in S19 (D-205): ordinary unit orders sent to a soldier inside a garrison are dropped.</summary>
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

    /// <summary>Control (S19 D-205): the same walk by a soldier that never garrisoned does leave and is shot at, so the Desired_ test below is not vacuous.</summary>
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

    [Theory]
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
