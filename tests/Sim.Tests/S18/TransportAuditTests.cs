// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Reflection;
using Rts.Sim.Core;
using Rts.Sim.Tests.M3.Systems;
using Rts.Sim.Tests.M7;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit: SIM-RULES-05 (splash reaches passengers) and SIM-RULES-06 (Deploy on a landing transport unloads at once).</summary>
public sealed class TransportAuditTests
{
    private static readonly Command[] None = Array.Empty<Command>();

    // ---- SIM-RULES-05 --------------------------------------------------------------------------

    /// <summary>A truck with two riflemen aboard and an enemy "crawler" (death weapon: 50 damage, splash 1) at the given distance east.</summary>
    private static (Simulation Sim, EntityId Truck, EntityId[] Riders, EntityId Crawler) Loaded(int crawlerDx)
    {
        Simulation sim = SystemsFixture.NewSim();
        EntityId truck = sim.SpawnVehicle(0, SystemsFixture.Truck, new CellCoord(10, 10));
        var riders = new[] { sim.SpawnInfantry(0, SystemsFixture.Rifle, new CellCoord(11, 10)), sim.SpawnInfantry(0, SystemsFixture.Rifle, new CellCoord(9, 10)) };
        EntityId crawler = sim.SpawnVehicle(1, SystemsFixture.Crawler, new CellCoord(10 + crawlerDx, 12));
        SystemsFixture.Run(sim, 1, riders.Select(r => SystemsFixture.Enter(sim, 0, ActorKind.Infantry, r, ActorKind.Vehicle, truck)).ToArray());
        foreach (EntityId r in riders) Assert.Equal(ActorLayer.Passenger, sim.LayerOf(ActorKind.Infantry, r.Index));
        SystemsFixture.Run(sim, 1, SystemsFixture.Move(sim, 0, ActorKind.Vehicle, truck, 10, 11));   // one cell south: now next to the crawler row
        SystemsFixture.Run(sim, 60);
        Assert.Equal(new CellCoord(10, 11), sim.Units.Position[truck.Index].ToCell());
        return (sim, truck, riders, crawler);
    }

    [Fact]
    public void Desired_Splash_SparesPassengers()
    {
        var (sim, _, riders, crawler) = Loaded(crawlerDx: 0);
        SystemsFixture.Run(sim, 2, SystemsFixture.Kill(sim, ActorKind.Vehicle, crawler));
        foreach (EntityId r in riders) Assert.Equal(50, sim.Infantry.Health[r.Index]);
    }

    // ---- SIM-RULES-06 --------------------------------------------------------------------------

    private const int Craft = 0, Hook = 2, Marine = 0, PerPassenger = 5;   // LandingTests' type indexes and unload_ticks

    /// <summary>LandingTests' coast map and rules (private there; borrowed by reflection so this audit adds no fixture of its own).</summary>
    private static SimSetup LandingSetup() =>
        (SimSetup)typeof(LandingTests).GetMethod("Setup", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;

    private static (Simulation Sim, EntityId Transport, EntityId[] Marines) LoadedTransport(int type)
    {
        var s = new Simulation(LandingSetup());
        EntityId c = s.SpawnVehicle(0, type, new CellCoord(9, 10));
        var marines = new EntityId[3];
        for (int k = 0; k < 3; k++) marines[k] = s.SpawnInfantry(0, Marine, new CellCoord(10, 9 + k));
        ushort seq = 0;
        foreach (EntityId m in marines)
            s.Step(new[] { new Command(s.Tick, 0, ++seq, CommandKind.Enter, m, c, default, (int)ActorKind.Infantry | ((int)ActorKind.Vehicle << 8)) });
        Assert.Equal(3, s.PassengersIn(c));
        return (s, c, marines);
    }

    private static Command Order(Simulation s, CommandKind kind, EntityId who, int x = 0, int y = 0) =>
        new(s.Tick, 0, 100, kind, who, default, new CellCoord(x, y), (int)ActorKind.Vehicle);

    /// <summary>Ticks after the order at which the number aboard changed.</summary>
    private static List<int> LeaveTicks(Simulation s, EntityId craft, Command order)
    {
        var ticks = new List<int>();
        int before = s.PassengersIn(craft), start = s.Tick;
        s.Step(new[] { order });
        for (int t = 0; t < 80; t++)
        {
            if (s.PassengersIn(craft) != before) { ticks.Add(s.Tick - start); before = s.PassengersIn(craft); }
            if (before == 0) break;
            s.Step(None);
        }
        return ticks;
    }

    [Fact]
    public void Control_UnloadOrder_LandsOneMarinePerUnloadTicks()
    {
        var (s, craft, _) = LoadedTransport(Craft);
        List<int> ticks = LeaveTicks(s, craft, Order(s, CommandKind.Unload, craft, 10, 10));
        Assert.Equal(3, ticks.Count);
        Assert.Equal(PerPassenger, ticks[1] - ticks[0]);
        Assert.Equal(PerPassenger, ticks[2] - ticks[1]);
    }

    // S18 audit: pins current behaviour of SIM-RULES-06; flip the assertion when fixed.
    [Fact]
    public void Audit_DeployOrder_OnALandingCraft_PutsEveryoneAshoreInOneTick()
    {
        var (s, craft, marines) = LoadedTransport(Craft);
        List<int> ticks = LeaveTicks(s, craft, Order(s, CommandKind.Deploy, craft));
        Assert.Equal(new[] { 1 }, ticks);                                 // all three left in the tick of the order
        foreach (EntityId m in marines) Assert.Equal(ActorLayer.Surface, s.Tunnels.InfantryLayer[m.Index]);
        Assert.Equal(0, s.Naval.UnloadTicksLeft[craft.Index]);
    }

    // S18 audit: pins current behaviour of SIM-RULES-06; flip the assertion when fixed.
    [Fact]
    public void Audit_DeployOrder_OnAFlyingSkyhook_DropsTheSquadMidFlightInOneTick()
    {
        var (s, hook, marines) = LoadedTransport(Hook);
        s.Step(new[] { Order(s, CommandKind.Move, hook, 25, 25) });
        for (int t = 0; t < 12; t++) s.Step(None);
        CellCoord at = s.Units.Position[hook.Index].ToCell();
        Assert.NotEqual(new CellCoord(25, 25), at);                       // still on its way
        Assert.NotEqual(new CellCoord(9, 10), at);
        List<int> ticks = LeaveTicks(s, hook, Order(s, CommandKind.Deploy, hook));
        Assert.Equal(new[] { 1 }, ticks);
        foreach (EntityId m in marines) Assert.Equal(ActorLayer.Surface, s.Tunnels.InfantryLayer[m.Index]);
        for (int t = 0; t < 20; t++) s.Step(None);
        Assert.NotEqual(at, s.Units.Position[hook.Index].ToCell());       // and it never stopped flying
    }

    [Fact(Skip = "S18 SIM-RULES-06: awaiting owner decision")]
    public void Desired_DeployOrder_OnALandingCraft_IsTimedLikeUnload()
    {
        var (s, craft, _) = LoadedTransport(Craft);
        List<int> ticks = LeaveTicks(s, craft, Order(s, CommandKind.Deploy, craft));
        Assert.Equal(3, ticks.Count);   // or 0, if Deploy is refused for landing transports
        Assert.Equal(PerPassenger, ticks[1] - ticks[0]);
    }
}
