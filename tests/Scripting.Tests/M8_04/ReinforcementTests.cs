// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.M8_04;

/// <summary>M8-04: Reinforce.Zone / Transport / Delayed / Naval / Rig put the right units in the right place at the right time.</summary>
public sealed class ReinforcementTests
{
    private static CellCoord CellOfInfantry(Simulation sim, int slot) => sim.Infantry.Position[slot].ToCell();

    private static IEnumerable<int> InfantrySlots(Simulation sim, int player, string type)
    {
        int ix = SimRules.IndexOf(sim.Setup.Rules.Infantry!, type, t => t.Id);
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player && sim.Infantry.TypeIndex[i] == ix) yield return i;
    }

    private static IEnumerable<int> VehicleSlots(Simulation sim, int player, string type)
    {
        int ix = SimRules.IndexOf(sim.Setup.Rules.Vehicles, type, t => t.Id);
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == ix) yield return i;
    }

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    [Fact]
    public void Zone_spawns_inside_the_zone_and_sends_the_units_to_the_destination()
    {
        const string Lua = @"function OnInit()
  state.tags = Reinforce.Zone(0, { 'auth_rifleman', 'auth_jeep', 'auth_rifleman' }, 'land', { x = 20, y = 25 })
end";
        var (sim, host, _) = World.Build(Lua);
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.Equal("3", TestAccess.StateValue(host, "tags").Split(',').Length.ToString());
        var zone = new MissionZone(15, 5, 19, 9);
        foreach (int s in InfantrySlots(sim, 0, "auth_rifleman")) Assert.True(zone.Contains(CellOfInfantry(sim, s)));
        foreach (int s in VehicleSlots(sim, 0, "auth_jeep")) Assert.True(zone.Contains(sim.Units.Position[s].ToCell()));
        Assert.Equal(2, InfantrySlots(sim, 0, "auth_rifleman").Count());
        Assert.Single(VehicleSlots(sim, 0, "auth_jeep"));

        TestData.Run(sim, 600);
        foreach (int s in VehicleSlots(sim, 0, "auth_jeep")) Assert.True(Cheb(sim.Units.Position[s].ToCell(), new CellCoord(20, 25)) <= 3);
    }

    [Fact]
    public void Zone_rejects_a_building_type_and_an_unknown_zone_with_the_function_name()
    {
        var (sim, host, _) = World.Build("function OnInit() Reinforce.Zone(0, { 'auth_power' }, 'land', { x = 20, y = 25 }) end");
        TestData.Run(sim, 1);
        Assert.Contains(host.Errors, e => e.Contains("Reinforce.Zone") && e.Contains("building"));
        var (sim2, host2, _) = World.Build("function OnInit() Reinforce.Zone(0, { 'auth_jeep' }, 'nowhere', { x = 20, y = 25 }) end");
        TestData.Run(sim2, 1);
        Assert.Contains(host2.Errors, e => e.Contains("Reinforce.Zone") && e.Contains("nowhere"));
    }

    [Fact]
    public void Delayed_spawns_nothing_before_the_delay_and_the_units_at_the_edge_after_it()
    {
        const string Lua = "function OnInit() Reinforce.Delayed(100, 1, { 'auth_jeep', 'auth_jeep' }, { x = 39, y = 15 }, { x = 20, y = 15 }) end";
        var (sim, host, _) = World.Build(Lua);
        TestData.Run(sim, 90);
        Assert.Empty(host.Errors);
        Assert.Equal(1, host.PendingArrivalCount);
        Assert.Empty(VehicleSlots(sim, 1, "auth_jeep"));
        TestData.Run(sim, 20);
        Assert.Equal(0, host.PendingArrivalCount);
        int[] jeeps = VehicleSlots(sim, 1, "auth_jeep").ToArray();
        Assert.Equal(2, jeeps.Length);
        foreach (int s in jeeps) Assert.True(sim.Units.Position[s].ToCell().X >= 36);
        TestData.Run(sim, 300);
        foreach (int s in jeeps) Assert.True(sim.Units.Position[s].ToCell().X < 36);   // they are driving in
    }

    [Fact]
    public void Transport_carries_the_soldiers_in_and_unloads_them_at_the_destination()
    {
        const string Lua = @"function OnInit()
  state.tags = Reinforce.Transport(0, { 'auth_rifleman', 'auth_rifleman', 'auth_rifleman', 'auth_jeep' }, { x = 39, y = 15 }, { x = 22, y = 15 })
end";
        var (sim, host, _) = World.Build(Lua);
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        int[] carriers = VehicleSlots(sim, 0, "auth_carrier").ToArray();
        Assert.Single(carriers);
        EntityId carrier = sim.Units.Slots.IdOf(carriers[0]);
        Assert.Equal(3, sim.PassengersIn(carrier));
        Assert.Equal(1, host.PendingArrivalCount);
        Assert.Single(VehicleSlots(sim, 0, "auth_jeep"));

        bool unloaded = false;
        for (int t = 0; t < 2500 && !unloaded; t++) { TestData.Run(sim, 1); unloaded = sim.PassengersIn(carrier) == 0; }
        Assert.True(unloaded);
        Assert.Equal(0, host.PendingArrivalCount);
        Assert.True(Cheb(sim.Units.Position[carriers[0]].ToCell(), new CellCoord(22, 15)) <= 2);
        foreach (int s in InfantrySlots(sim, 0, "auth_rifleman")) Assert.True(Cheb(CellOfInfantry(sim, s), new CellCoord(22, 15)) <= 5);
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Transport_with_no_soldiers_spawns_no_transport()
    {
        var (sim, host, _) = World.Build("function OnInit() Reinforce.Transport(0, { 'auth_jeep' }, { x = 39, y = 15 }, { x = 22, y = 15 }) end");
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.Empty(VehicleSlots(sim, 0, "auth_carrier"));
        Assert.Equal(0, host.PendingArrivalCount);
    }

    [Fact]
    public void Naval_arrives_by_sea_and_lands_its_marines_for_an_expedition_player()
    {
        const string Lua = @"function OnInit()
  state.tags = Reinforce.Naval(0, { 'exp_landing_craft', 'exp_marine', 'exp_marine' }, { x = 0, y = 15 }, { x = 10, y = 15 })
end";
        var (sim, host, _) = World.Build(Lua, Faction.Expedition, sea: true);
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        int[] craft = VehicleSlots(sim, 0, "exp_landing_craft").ToArray();
        Assert.Single(craft);
        EntityId id = sim.Units.Slots.IdOf(craft[0]);
        Assert.True(sim.Units.Position[craft[0]].ToCell().X <= 2);   // starts at the sea edge, on water
        Assert.Equal(LandType.Water, sim.Map.LandAt(sim.Units.Position[craft[0]].ToCell()));
        Assert.Equal(2, sim.PassengersIn(id));

        bool empty = false;
        for (int t = 0; t < 2500 && !empty; t++) { TestData.Run(sim, 1); empty = sim.PassengersIn(id) == 0; }
        Assert.True(empty);
        int[] marines = InfantrySlots(sim, 0, "exp_marine").ToArray();
        Assert.Equal(2, marines.Length);
        foreach (int m in marines)
        {
            CellCoord c = CellOfInfantry(sim, m);
            Assert.Equal(LandType.Clear, sim.Map.LandAt(c));
            Assert.True(Cheb(c, new CellCoord(10, 15)) <= 3);
        }

        Assert.Equal(LandType.Water, sim.Map.LandAt(sim.Units.Position[craft[0]].ToCell()));   // the craft stays at sea
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Naval_ships_without_cargo_sail_to_the_sea_cell_nearest_the_destination_and_cargo_needs_a_ship()
    {
        var (sim, host, _) = World.Build("function OnInit() Reinforce.Naval(0, { 'exp_landing_craft' }, { x = 0, y = 5 }, { x = 30, y = 15 }) end", Faction.Expedition, sea: true);
        TestData.Run(sim, 600);
        Assert.Empty(host.Errors);
        int[] craft = VehicleSlots(sim, 0, "exp_landing_craft").ToArray();
        Assert.Single(craft);
        Assert.True(sim.Units.Position[craft[0]].ToCell().X >= 8);   // nearest water is the east shore of the sea

        var (sim2, host2, _) = World.Build("function OnInit() Reinforce.Naval(0, { 'exp_marine' }, { x = 0, y = 5 }, { x = 10, y = 15 }) end", Faction.Expedition, sea: true);
        TestData.Run(sim2, 1);
        Assert.Contains(host2.Errors, e => e.Contains("Reinforce.Naval") && e.Contains("no ship"));
    }

    [Fact]
    public void Rig_arrives_at_the_edge_and_unpacks_into_a_mother_rig_for_left_behind()
    {
        const string Lua = "function OnInit() state.rig = Reinforce.Rig(0, { x = 39, y = 10 }, { x = 25, y = 10 }) end";
        var (sim, host, _) = World.Build(Lua, Faction.LeftBehind);
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        int[] rigs = VehicleSlots(sim, 0, "lb_mother_rig_packed").ToArray();
        Assert.Single(rigs);
        Assert.True(sim.Units.Position[rigs[0]].ToCell().X >= 36);
        int motherType = SimRules.IndexOf(sim.Setup.Rules.Buildings!, "lb_mother_rig", t => t.Id);
        bool Built() { for (int b = 1; b < sim.Buildings.Capacity; b++) if (sim.Buildings.Slots.IsIndexAlive(b) && sim.Buildings.Player[b] == 0 && sim.Buildings.TypeIndex[b] == motherType) return true; return false; }
        for (int t = 0; t < 3000 && !Built(); t++) TestData.Run(sim, 1);
        Assert.True(Built());
        Assert.Empty(VehicleSlots(sim, 0, "lb_mother_rig_packed"));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Edge_cells_off_the_map_edge_are_still_rejected()
    {
        foreach (string call in new[]
        {
            "Reinforce.Transport(0, { 'auth_rifleman' }, { x = 5, y = 5 }, { x = 22, y = 15 })",
            "Reinforce.Delayed(10, 0, { 'auth_rifleman' }, { x = 5, y = 5 }, { x = 22, y = 15 })",
            "Reinforce.Naval(0, { 'exp_landing_craft' }, { x = 5, y = 5 }, { x = 22, y = 15 })",
            "Reinforce.Rig(0, { x = 5, y = 5 }, { x = 22, y = 15 })",
        })
        {
            var (sim, host, _) = World.Build("function OnInit() " + call + " end");
            TestData.Run(sim, 1);
            Assert.Contains(host.Errors, e => e.Contains("not on the map edge"));
        }
    }

    [Fact]
    public void Save_in_the_middle_of_a_delay_or_a_transport_drive_reproduces_the_final_hash()
    {
        const string Lua = @"function OnInit()
  Reinforce.Delayed(150, 1, { 'auth_jeep', 'auth_rifleman' }, { x = 39, y = 15 }, { x = 20, y = 15 })
  Reinforce.Transport(0, { 'auth_rifleman', 'auth_rifleman' }, { x = 39, y = 20 }, { x = 22, y = 20 })
end";
        var (a, hostA, def) = World.Build(Lua);
        TestData.Run(a, 60);
        Assert.Equal(2, hostA.PendingArrivalCount);
        (Simulation b, MissionHost hostB) = World.Reload(a, def, Lua, Faction.Authority, false);
        Assert.Equal(2, hostB.PendingArrivalCount);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        TestData.Run(a, 1500);
        TestData.Run(b, 1500);
        Assert.Equal(0, hostA.PendingArrivalCount);
        Assert.Equal(0, hostB.PendingArrivalCount);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
        Assert.Empty(hostA.Errors);
        Assert.Empty(hostB.Errors);
        Assert.Single(VehicleSlots(a, 1, "auth_jeep"));
    }
}
