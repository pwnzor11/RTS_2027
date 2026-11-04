// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;
using static Rts.Sim.Tests.M3.Burrow.BurrowWorld;

namespace Rts.Sim.Tests.M3.Burrow;

/// <summary>M3-03: the Bore digs a Hatch (5 s telegraph), the Hatch upgrades in place to a Bore Head, the land-train deploys
/// into a Command Shaft (design 05 §1, §4).</summary>
public sealed class BoreAndMorphTests
{
    private static EntityId HatchAt(Simulation sim, CellCoord c) => sim.Map.BuildingAt(c);

    // ---- Bore -> Hatch --------------------------------------------------------------------------------

    [Fact]
    public void Bore_TravelsBurrowed_ThenDigsAHatchBehindAFiveSecondTelegraph()
    {
        var sim = NewSim();
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        var target = new CellCoord(30, 12);
        Step(sim, DeployAt(sim, bore, target.X, target.Y));
        RunUntil(sim, () => Layer(sim, bore) == ActorLayer.Burrowed);       // it dived first
        Assert.True(EntranceTelegraph(sim) is null);
        RunUntil(sim, () => EntranceTelegraph(sim) != null);

        Assert.False(sim.Units.Slots.IsAlive(bore), "the Bore becomes the Hatch");
        Telegraph tg = EntranceTelegraph(sim)!.Value;
        Assert.Equal(sim.Setup.Rules.Tunnels.EntranceTelegraphTicks, tg.TicksLeft);
        Assert.Equal(100, tg.TicksLeft);                                    // 5 s
        Assert.Equal(target, tg.Cell);
        Assert.Equal(0, tg.Owner);
        EntityId hatch = HatchAt(sim, target);
        Assert.False(hatch.IsNone);
        Assert.Equal(new ActorRef(ActorKind.Building, hatch), tg.Subject);
        Assert.Equal(Hatch, sim.Buildings.TypeIndex[hatch.Index]);
        Assert.Equal((byte)0, sim.Buildings.Player[hatch.Index]);
        Assert.Equal(50, sim.Buildings.Health[hatch.Index]);                // 250 per mille of 200
        Assert.True(sim.IsEmergingEntrance(hatch.Index));

        int shown = 0;
        while (EntranceTelegraph(sim) is { } t)
        {
            shown++;
            Assert.True(sim.IsEmergingEntrance(hatch.Index));
            Assert.Contains(sim.Snapshot(0).Telegraphs, v => v.Kind == TelegraphKind.Entrance && v.Cell == target);
            sim.Step(None);
        }
        Assert.Equal(100, shown);
        Assert.False(sim.IsEmergingEntrance(hatch.Index));
        Assert.Equal(Hatch, sim.Buildings.TypeIndex[hatch.Index]);
        Assert.Equal(50, sim.Buildings.Health[hatch.Index]);                // it keeps the low health until repaired
    }

    [Fact]
    public void Bore_OnHardGround_DigsOnTheNearestSoftCell_AndKeepsAClearRing()
    {
        var sim = NewSim((land, w) => { for (int y = 6; y <= 14; y++) for (int x = 28; x <= 32; x++) land[y * w + x] = LandType.Road; });
        sim.PlaceBuilding(1, Block, new CellCoord(35, 10));
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        Step(sim, DeployAt(sim, bore, 34, 10));                              // right next to the block
        RunUntil(sim, () => EntranceTelegraph(sim) != null);
        CellCoord cell = EntranceTelegraph(sim)!.Value.Cell;
        Assert.True(IsSoft(sim, cell));
        EntityId hatch = HatchAt(sim, cell);
        Assert.False(hatch.IsNone);
        Assert.True(Cheb(cell, new CellCoord(35, 10)) >= 2);

        // on hardened ground nothing may break through either
        var sim2 = NewSim();
        sim2.Ground.Hardened[sim2.Ground.IndexOf(new CellCoord(30, 10))] = 1;
        EntityId bore2 = sim2.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        Step(sim2, DeployAt(sim2, bore2, 30, 10));
        RunUntil(sim2, () => EntranceTelegraph(sim2) != null);
        Assert.NotEqual(new CellCoord(30, 10), EntranceTelegraph(sim2)!.Value.Cell);
    }

    [Fact]
    public void Bore_WithNoRoomForAHatch_SurfacesAsAUnit()
    {
        var sim = NewSim();
        // harden the whole map: nothing can surface or break through anywhere; the Bore keeps waiting underground
        for (int k = 0; k < sim.Ground.Hardened.Length; k++) sim.Ground.Hardened[k] = 1;
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        Step(sim, DeployAt(sim, bore, 20, 10));
        Run(sim, 200);
        Assert.True(sim.Units.Slots.IsAlive(bore));
        Assert.Equal(ActorLayer.Burrowed, Layer(sim, bore));
        Assert.Empty(sim.Tunnels.Telegraphs);
        // re-open the ground: it comes up as a Bore (no Hatch dug)
        Array.Clear(sim.Ground.Hardened);
        RunUntil(sim, () => Layer(sim, bore) == ActorLayer.Surface);
        Assert.True(sim.Units.Slots.IsAlive(bore));
    }

    // ---- Hatch -> Bore Head ---------------------------------------------------------------------------

    private static (Simulation Sim, EntityId Hatch) HatchSim()
    {
        var sim = NewSim();
        EntityId h = sim.PlaceBuilding(0, Hatch, new CellCoord(10, 10));
        Assert.False(h.IsNone);
        return (sim, h);
    }

    private static Command Upgrade(Simulation sim, EntityId b, int type, byte player = 0) =>
        Cmd(sim, CommandKind.Upgrade, b, default, type, player);

    [Fact]
    public void Hatch_UpgradesInPlaceToBoreHead_PayingTheDifference()
    {
        (Simulation sim, EntityId h) = HatchSim();
        int cash = sim.Players.Cash[0];
        sim.Buildings.Health[h.Index] = 100;                                 // half health
        Step(sim, Upgrade(sim, h, BoreHead));
        Assert.Equal(cash - 300, sim.Players.Cash[0]);                       // 400 - 100
        Assert.Equal(BoreHead + 1, sim.Tunnels.BuildingMorphType[h.Index]);
        int expected = SimRules.BuildTicks(300, 0, sim.Setup.Rules.Economy);
        Assert.Equal(expected - 1, sim.Tunnels.BuildingMorphTicks[h.Index]); // one tick has run
        // still a working Hatch until the morph is done
        Run(sim, expected - 3);
        Assert.Equal(Hatch, sim.Buildings.TypeIndex[h.Index]);
        Assert.Equal(TunnelSize.S, sim.Setup.Rules.Buildings[sim.Buildings.TypeIndex[h.Index]].Entrance);
        RunUntil(sim, () => sim.Buildings.TypeIndex[h.Index] == BoreHead, 20);
        Assert.Equal(0, sim.Tunnels.BuildingMorphType[h.Index]);
        Assert.Equal(0, sim.Tunnels.BuildingMorphTicks[h.Index]);
        Assert.Equal(h, HatchAt(sim, new CellCoord(10, 10)));                // same building, same slot, same cell
        Assert.Equal(300, sim.Buildings.Health[h.Index]);                    // half of 200 -> half of 600
        Assert.Equal(cash - 300, sim.Players.Cash[0]);
    }

    [Fact]
    public void Upgrade_IsRefusedWhenItCannotWork()
    {
        (Simulation sim, EntityId h) = HatchSim();
        int cash = sim.Players.Cash[0];
        void Refused(Command c)
        {
            Step(sim, c);
            Assert.Equal(0, sim.Tunnels.BuildingMorphType[h.Index]);
            Assert.Equal(cash, sim.Players.Cash[0]);
        }
        Refused(Upgrade(sim, h, BoreHead, 1));                               // not the owner's
        Refused(Upgrade(sim, h, Hatch));                                     // same type
        Refused(Upgrade(sim, h, Shaft));                                     // different footprint
        Refused(Upgrade(sim, h, Block));                                     // not an entrance class increase
        Refused(Upgrade(sim, h, 99));                                        // no such type
        sim.Players.Cash[0] = 299;
        cash = 299;
        Refused(Upgrade(sim, h, BoreHead));                                  // cannot pay
        sim.Players.Cash[0] = 5000;
        cash = 5000;
        Step(sim, Upgrade(sim, h, BoreHead));
        Assert.Equal(BoreHead + 1, sim.Tunnels.BuildingMorphType[h.Index]);
        Assert.Equal(4700, sim.Players.Cash[0]);
        Step(sim, Upgrade(sim, h, BoreHead));                                // already morphing: not charged twice
        Assert.Equal(4700, sim.Players.Cash[0]);
        // a Bore Head does not go back to a Hatch
        RunUntil(sim, () => sim.Buildings.TypeIndex[h.Index] == BoreHead);
        Step(sim, Upgrade(sim, h, Hatch));
        Assert.Equal(0, sim.Tunnels.BuildingMorphType[h.Index]);
        Assert.Equal(BoreHead, sim.Buildings.TypeIndex[h.Index]);
    }

    [Fact]
    public void Upgrade_IsRefusedWhileTheHatchIsStillEmerging()
    {
        var sim = NewSim();
        EntityId bore = sim.SpawnVehicle(0, Bore, new CellCoord(10, 10));
        Step(sim, DeployAt(sim, bore, 20, 10));
        RunUntil(sim, () => EntranceTelegraph(sim) != null);
        EntityId h = HatchAt(sim, EntranceTelegraph(sim)!.Value.Cell);
        int cash = sim.Players.Cash[0];
        Step(sim, Upgrade(sim, h, BoreHead));
        Assert.Equal(0, sim.Tunnels.BuildingMorphType[h.Index]);
        Assert.Equal(cash, sim.Players.Cash[0]);
        RunUntil(sim, () => EntranceTelegraph(sim) == null);
        Step(sim, Upgrade(sim, h, BoreHead));
        Assert.Equal(BoreHead + 1, sim.Tunnels.BuildingMorphType[h.Index]);
    }

    [Fact]
    public void Morph_IsDroppedWhenTheBuildingIsSold()
    {
        (Simulation sim, EntityId h) = HatchSim();
        Step(sim, Upgrade(sim, h, BoreHead));
        Step(sim, Cmd(sim, CommandKind.Sell, h, default, (int)ActorKind.Building));
        Assert.False(sim.Buildings.Slots.IsAlive(h));
        Assert.Equal(0, sim.Tunnels.BuildingMorphType[h.Index]);
        Run(sim, 100);   // nothing left to morph, nothing throws
    }

    // ---- land-train -----------------------------------------------------------------------------------

    [Fact]
    public void LandTrain_DeploysInPlaceIntoTheCommandShaft_KeepingItsHealthFraction()
    {
        var sim = NewSim();
        EntityId train = sim.SpawnVehicle(0, Train, new CellCoord(20, 20));
        sim.Units.Health[train.Index] = 200;                                 // half of 400
        Step(sim, Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle));
        Assert.False(sim.Units.Slots.IsAlive(train));
        Assert.True(sim.Map.VehicleAt(new CellCoord(20, 20)).IsNone);
        EntityId shaft = sim.Map.BuildingAt(new CellCoord(20, 20));
        Assert.False(shaft.IsNone);
        Assert.Equal(Shaft, sim.Buildings.TypeIndex[shaft.Index]);
        Assert.Equal(new CellCoord(19, 19), sim.Buildings.Cell[shaft.Index]);   // 3x3 centred on the vehicle's cell
        Assert.Equal(500, sim.Buildings.Health[shaft.Index]);
        Assert.Equal((byte)0, sim.Buildings.Player[shaft.Index]);
        for (int y = 19; y <= 21; y++) for (int x = 19; x <= 21; x++) Assert.Equal(shaft, sim.Map.BuildingAt(new CellCoord(x, y)));
        Assert.Empty(sim.Tunnels.Telegraphs);                                // a normal deploy: no telegraph, a finished entrance
        Assert.False(sim.IsEmergingEntrance(shaft.Index));

        // one way: deploying the building (or the stale vehicle id) brings nothing back
        Step(sim, Cmd(sim, CommandKind.Deploy, shaft, default, (int)ActorKind.Building));
        Step(sim, Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle));
        Assert.Equal(0, sim.Units.Slots.Count);
        Assert.Equal(shaft, sim.Map.BuildingAt(new CellCoord(20, 20)));
    }

    [Fact]
    public void LandTrain_DoesNotDeployWhereTheFootprintIsBlocked()
    {
        var sim = NewSim((land, w) => land[20 * w + 22] = LandType.Rough);   // rough is not buildable
        EntityId train = sim.SpawnVehicle(0, Train, new CellCoord(20, 20));
        EntityId other = sim.SpawnVehicle(1, Enemy, new CellCoord(21, 20));  // an enemy tank inside the footprint
        Step(sim, Cmd(sim, CommandKind.Deploy, train, default, (int)ActorKind.Vehicle));
        Assert.True(sim.Units.Slots.IsAlive(train));
        Assert.True(sim.Map.BuildingAt(new CellCoord(20, 20)).IsNone);

        var sim2 = NewSim((land, w) => land[20 * w + 21] = LandType.Rough);  // unbuildable ground inside the footprint
        EntityId t2 = sim2.SpawnVehicle(0, Train, new CellCoord(20, 20));
        Step(sim2, Cmd(sim2, CommandKind.Deploy, t2, default, (int)ActorKind.Vehicle));
        Assert.True(sim2.Units.Slots.IsAlive(t2));

        var sim3 = NewSim();
        EntityId t3 = sim3.SpawnVehicle(0, Train, new CellCoord(0, 0));      // footprint hangs off the map
        Step(sim3, Cmd(sim3, CommandKind.Deploy, t3, default, (int)ActorKind.Vehicle));
        Assert.True(sim3.Units.Slots.IsAlive(t3));

        var sim4 = NewSim();
        EntityId t4 = sim4.SpawnVehicle(0, Train, new CellCoord(20, 20));
        Step(sim4, Cmd(sim4, CommandKind.Deploy, t4, default, (int)ActorKind.Vehicle, 1));   // not the owner
        Assert.True(sim4.Units.Slots.IsAlive(t4));
        _ = other;
    }
}
