// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.Tests.Determinism;

namespace Rts.Sim.Tests.S18;

/// <summary>S18 audit (SIM-RULES-07): a small rig packed into the one generic packed vehicle is checked against that vehicle's own
/// unpacks_into (the 3x3 mother rig) by RigMission, although the Pack command and FinishUnpack use the type it was packed from.
/// Buildings (ScriptedGame): 0 barracks 2x2, 1 hq 3x3 (the "mother rig"), 2 turret 1x1 (the "lookout").</summary>
public sealed class RigUnpackAuditTests
{
    private const int Barracks = 0, Mother = 1, Lookout = 2, Rig = 0, PackN = 20;

    private static (Simulation Sim, EntityId Rig) PackedLookout(bool neighbour)
    {
        VehicleType[] vehicles = { ScriptedGame.Vehicle("rig", 11, 200) with { UnpacksInto = Mother, PackTicks = PackN } };
        SimRules rules = ScriptedGame.RulesWith(vehicles, Array.Empty<InfantryType>());
        var s = new Simulation(new SimSetup(1, MapData.CreateFlat(64, 64), 2, rules, 0xABCDUL) { AllowDebug = true });
        if (neighbour) Assert.False(s.PlaceBuilding(0, Barracks, new CellCoord(33, 30)).IsNone);   // inside the 3x3 at (31,30), clear of the 1x1
        EntityId rig = s.SpawnVehicle(0, Rig, new CellCoord(28, 30));
        s.Rigs.PackedFrom[rig.Index] = Lookout + 1;   // it was packed from a 1x1 lookout
        s.Rigs.HpPermille[rig.Index] = 1000;
        return (s, rig);
    }

    private static readonly CellCoord Site = new(31, 30);

    private static void OrderUnpack(Simulation s, EntityId rig) =>
        s.Step(new[] { new Command(s.Tick, 0, 1, CommandKind.Pack, rig, default, Site, 0) });

    [Fact]
    public void Control_LookoutRig_UnpacksOnOpenGround()
    {
        var (s, rig) = PackedLookout(neighbour: false);
        OrderUnpack(s, rig);
        for (int t = 0; t < 600 && s.Units.Slots.IsAlive(rig); t++) s.Step(Array.Empty<Command>());
        Assert.False(s.Units.Slots.IsAlive(rig));
        EntityId b = s.Map.BuildingAt(Site);
        Assert.False(b.IsNone);
        Assert.Equal(Lookout, s.Buildings.TypeIndex[b.Index]);
    }

    [Fact]
    public void Desired_LookoutRig_NextToABuilding_Unpacks()
    {
        var (s, rig) = PackedLookout(neighbour: true);
        OrderUnpack(s, rig);
        for (int t = 0; t < 600 && s.Units.Slots.IsAlive(rig); t++) s.Step(Array.Empty<Command>());
        Assert.False(s.Units.Slots.IsAlive(rig));
        Assert.Equal(Lookout, s.Buildings.TypeIndex[s.Map.BuildingAt(Site).Index]);
    }
}
