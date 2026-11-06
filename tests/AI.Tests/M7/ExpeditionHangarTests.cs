// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Data;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace AI.Tests.M7;

/// <summary>S12 (D-156): the Expedition AI launches a carrier only when every docked aircraft holds full ammunition; the view tells it so for own units only.</summary>
public sealed class ExpeditionHangarTests
{
    private static int Vehicle(string id) => SimRules.IndexOf(TestWorld.Rules.Sim.Vehicles, id, v => v.Id);

    /// <summary>A sim with an own carrier and two jets docked in it; the enemy side has a jet too.</summary>
    private static (Simulation Sim, EntityId Carrier, EntityId JetA, EntityId JetB) DockedPair()
    {
        (MapData map, MapMeta meta) = ExpeditionRig.OceanMap();
        var sim = new Simulation(new SimSetup(1, map, 2, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash)
            { Factions = [Faction.Expedition, Faction.Authority], AllowDebug = true, LandingMarkers = meta.Landing });
        CellCoord sea = default;
        for (int y = 0; y < map.Height && sea == default; y++)
            for (int x = 0; x < map.Width; x++)
                if (map.Land[y * map.Width + x] == LandType.Water && sim.ViewFor(0).NavalRegionAt(new CellCoord(x, y), true) > 0) { sea = new CellCoord(x, y); break; }
        EntityId carrier = sim.SpawnVehicle(0, Vehicle("exp_carrier"), sea);
        EntityId a = sim.SpawnVehicle(0, Vehicle("exp_jet"), sea), b = sim.SpawnVehicle(0, Vehicle("exp_jet"), sea);
        ushort seq = 0;
        foreach (EntityId j in new[] { a, b })
            sim.Step([new Command(sim.Tick, 0, ++seq, CommandKind.Enter, j, carrier, default, (int)ActorKind.Vehicle | ((int)ActorKind.Vehicle << 8))]);
        for (int i = 0; i < 400; i++) sim.Step(Array.Empty<Command>());
        return (sim, carrier, a, b);
    }

    private static bool Full(Simulation sim, EntityId carrier)
    {
        var k = new RecruitKnowledge();
        k.Refresh(sim.ViewFor(0), "exp_landing_depot");
        foreach (ViewActor c in k.Own) if (c.Id == carrier) return ExpeditionTactics.HangarFull(k, c);
        throw new InvalidOperationException("carrier not in the own list");
    }

    [Fact]
    public void ACarrierIsNotLaunchedWhileADockedAircraftIsHalfRearmed()
    {
        (Simulation sim, EntityId carrier, EntityId a, EntityId b) = DockedPair();
        int max = TestWorld.Rules.Sim.Vehicles[Vehicle("exp_jet")].Ammo;
        Assert.True(max > 1);
        Assert.True(sim.ViewFor(0).OwnCarrierHangar(carrier, out int docked, out _) && docked == 2, "both jets docked");
        sim.Naval.AmmoLeft[a.Index] = max; sim.Naval.AmmoLeft[b.Index] = max;
        Assert.True(Full(sim, carrier), "both full: launch allowed");
        sim.Naval.AmmoLeft[b.Index] = max - 1;
        Assert.False(Full(sim, carrier), "one jet one round short: wait");
        sim.Naval.AmmoLeft[b.Index] = 0;
        Assert.False(Full(sim, carrier), "one jet dry: wait");
    }

    [Fact]
    public void TheViewShowsAmmoFullAndTheCarrierForOwnUnitsOnly()
    {
        (Simulation sim, EntityId carrier, EntityId a, _) = DockedPair();
        int max = TestWorld.Rules.Sim.Vehicles[Vehicle("exp_jet")].Ammo;
        sim.Naval.AmmoLeft[a.Index] = max - 1;
        var own = new List<ViewActor>();
        sim.ViewFor(0).CopyOwn(own);
        ViewActor jet = own.Single(x => x.Id == a);
        Assert.False(jet.AmmoFull);
        Assert.Equal(carrier, jet.DockedIn);
        // the enemy's view of the same aircraft never lists it (docked aircraft are hidden), and enemy actors default to full / not docked
        var seen = new List<ViewActor>();
        sim.ViewFor(1).CopyVisible(seen);
        foreach (ViewActor e in seen) { Assert.True(e.AmmoFull); Assert.Equal(EntityId.None, e.DockedIn); }
    }
}
