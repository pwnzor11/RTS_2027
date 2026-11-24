// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.Tunnels;

namespace Rts.Sim.Tests.M3.Tunnels;

/// <summary>A43 SIMMOVE-01/02: a vehicle leaving the network surfaces once, at the exit, owned by the tunnel code only; a
/// unit whose exit has no free cell resurfaces at the entrance node after a bounded wait.</summary>
public sealed class TunnelVehicleSurfaceTests
{
    private const int BoreHead = 0, Plain = 1, Crawler = 0;

    private static SimRules Rules()
    {
        var vehicles = new[] { ScriptedGame.Vehicle("dir_crawler", 30, 100) with { TunnelSize = TunnelSize.M } };
        var infantry = new[] { ScriptedGame.Infantry("dir_cultist", 9, 50) };
        var buildings = new[]
        {
            ScriptedGame.Building("dir_borehead", 2, 2, 400) with { Entrance = TunnelSize.M, TicksPerPoint = 13 },
            ScriptedGame.Building("plain", 2, 2, 300),
        };
        return new SimRules(vehicles, infantry, buildings, Array.Empty<WeaponType>(), Array.Empty<WarheadType>(),
            ScriptedGame.Terrain(), Array.Empty<EffectDef>());
    }

    private static Simulation NewSim()
    {
        var sim = new Simulation(new SimSetup(7, MapData.CreateFlat(64, 64), 2, Rules(), 0x7E57UL) { AllowDebug = true });
        Assert.False(sim.PlaceBuilding(0, Plain, new CellCoord(2, 56)).IsNone);
        Assert.False(sim.PlaceBuilding(1, Plain, new CellCoord(58, 2)).IsNone);
        return sim;
    }

    private static ushort _seq;

    private static Command Enter(Simulation sim, ActorRef unit, EntityId dest) =>
        new(sim.Tick, 0, _seq++, CommandKind.Enter, unit.Id, dest, default, (int)unit.Kind | ((int)ActorKind.Building << 8));

    private static ActorLayer Layer(Simulation sim, ActorRef u) => sim.LayerOf(u.Kind, u.Id.Index);

    private static int StepUntil(Simulation sim, Func<bool> done, int max = 1500)
    {
        for (int n = 0; n < max; n++)
        {
            sim.Step(Array.Empty<Command>());
            if (done()) return sim.Tick;
        }
        throw new Xunit.Sdk.XunitException("condition not reached");
    }

    [Fact]
    public void Vehicle_FullTunnelCycle_SurfacesOnceAtTheExit_AndLeavesNoPhantomClaim()
    {
        var sim = NewSim();
        sim.PlaceBuilding(0, BoreHead, new CellCoord(10, 10));
        EntityId dest = sim.PlaceBuilding(0, BoreHead, new CellCoord(40, 40));
        var u = new ActorRef(ActorKind.Vehicle, sim.SpawnVehicle(0, Crawler, new CellCoord(10, 13)));
        sim.Step(new[] { Enter(sim, u, dest) });
        int surfacing = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surfacing);
        var exit = new CellCoord(41, 42);
        Assert.Equal(exit, sim.Units.Position[u.Id.Index].ToCell());
        int surfaced = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.Equal(sim.Setup.Rules.Tunnels.TelegraphTicks, surfaced - surfacing);
        Assert.Equal(exit, sim.Units.Position[u.Id.Index].ToCell());
        Assert.Empty(sim.Tunnels.Telegraphs);
        Assert.Empty(sim.Tunnels.Transit);
        Assert.Equal(u.Id, sim.Map.VehicleAt(exit));
        // drive it away: no phantom may remain on the exit cell
        sim.Step(new[] { new Command(sim.Tick, 0, _seq++, CommandKind.Move, u.Id, default, new CellCoord(41, 55), (int)ActorKind.Vehicle) });
        StepUntil(sim, () => sim.Units.Position[u.Id.Index].ToCell().Y >= 50);
        Assert.True(sim.Map.VehicleAt(exit).IsNone);
    }

    // A blocked exit: every cell within ring 8 of the exit (41,42) of the hatch at (40,40) holds a (fake) vehicle claim.
    private static void BlockExit(Simulation sim, EntityId fake)
    {
        for (int y = 42 - 8; y <= 42 + 8; y++)
            for (int x = 41 - 8; x <= 41 + 8; x++)
                if (sim.Map.BuildingAt(new CellCoord(x, y)).IsNone) sim.Map.SetVehicle(new CellCoord(x, y), fake);
    }

    [Fact]
    public void BlockedExit_ResurfacesAtAnotherEntrance_AfterTheBoundedWait()
    {
        var sim = NewSim();
        EntityId origin = sim.PlaceBuilding(0, BoreHead, new CellCoord(10, 10));
        EntityId dest = sim.PlaceBuilding(0, BoreHead, new CellCoord(40, 40));
        var u = new ActorRef(ActorKind.Vehicle, sim.SpawnVehicle(0, Crawler, new CellCoord(10, 13)));
        var fake = new ActorRef(ActorKind.Vehicle, sim.SpawnVehicle(1, Crawler, new CellCoord(30, 30)));
        sim.Step(new[] { Enter(sim, u, dest) });
        int under = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Tunnel);
        BlockExit(sim, fake.Id);
        int surfacing = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surfacing);
        Assert.True(surfacing - under >= Simulation.TunnelExitWaitTicks, "waited the bounded time first");
        Assert.Equal(origin, sim.Tunnels.Transit.Single().Destination);
        Assert.True(sim.Units.Position[u.Id.Index].ToCell().X < 20, "surfaced at the other entrance, not the blocked one");
        StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surface);
        Assert.Empty(sim.Tunnels.Transit);
    }

    [Fact]
    public void BlockedExit_WithNoOtherEntrance_KeepsWaiting_ThenSurfacesOnceFree()
    {
        var sim = NewSim();
        EntityId dest = sim.PlaceBuilding(0, BoreHead, new CellCoord(40, 40));
        var u = new ActorRef(ActorKind.Vehicle, sim.SpawnVehicle(0, Crawler, new CellCoord(10, 13)));
        var fake = new ActorRef(ActorKind.Vehicle, sim.SpawnVehicle(1, Crawler, new CellCoord(30, 30)));
        // A single entrance cannot be entered from elsewhere: put the unit underground directly through a second hatch, then remove it.
        EntityId origin = sim.PlaceBuilding(0, BoreHead, new CellCoord(10, 10));
        sim.Step(new[] { Enter(sim, u, dest) });
        StepUntil(sim, () => Layer(sim, u) == ActorLayer.Tunnel);
        BlockExit(sim, fake.Id);
        sim.Step(new[] { new Command(sim.Tick, 0, _seq++, CommandKind.Debug, origin, default, default, (int)DebugOp.Kill, (int)ActorKind.Building) });
        for (int n = 0; n < Simulation.TunnelExitWaitTicks + 200; n++) sim.Step(Array.Empty<Command>());
        Assert.Equal(ActorLayer.Tunnel, Layer(sim, u));
        Assert.True(sim.Tunnels.Transit.Single().TicksLeft < -Simulation.TunnelExitWaitTicks);
        for (int y = 34; y <= 50; y++) for (int x = 33; x <= 49; x++) if (sim.Map.VehicleAt(new CellCoord(x, y)) == fake.Id) sim.Map.SetVehicle(new CellCoord(x, y), EntityId.None);
        int freed = sim.Tick;
        int surfacing = StepUntil(sim, () => Layer(sim, u) == ActorLayer.Surfacing);
        Assert.True(surfacing - freed <= Simulation.TunnelExitBackoffTicks);
    }
}
