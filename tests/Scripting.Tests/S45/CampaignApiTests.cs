// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting.Tests.M8_04;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S45;

/// <summary>S45 package cmp: Trigger.OnSuperweaponFired (C-05), Reinforce.Rig park-only (C-06), the lb_02 text (C-07) and the dir_04 zone names (C-10).</summary>
public sealed class CampaignApiTests
{
    private static int BuildingSlot(Simulation sim, int player, string typeId)
    {
        int type = SimRules.IndexOf(sim.Setup.Rules.Buildings!, typeId, b => b.Id);
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && sim.Buildings.Player[i] == player && sim.Buildings.TypeIndex[i] == type) return i;
        return -1;
    }

    private static void Fire(Simulation sim, byte player, int building, CellCoord target, ushort seq) =>
        sim.Step(new[] { new Command(sim.Tick, player, seq, CommandKind.Special, sim.Buildings.Slots.IdOf(building), default, target, 0, 0) });

    private const string ShotLua = @"
function OnInit()
  state.n = 0
  state.other = 0
  Actor.Create('lb_second_drift', 0, { x = 20, y = 20 }, 'sw')
  Trigger.OnSuperweaponFired(0, Shot)
  Trigger.OnSuperweaponFired(1, OtherShot)
end
function Shot(p, tag, cell)
  state.n = state.n + 1
  state.p = p
  state.tag = tag
  state.x = cell.x
  state.y = cell.y
end
function OtherShot() state.other = state.other + 1 end
";

    [Fact]
    public void Superweapon_fired_trigger_runs_once_per_shot_with_player_tag_and_cell()
    {
        var (sim, host) = TestData.Tiny(ShotLua);
        TestData.Run(sim, 3);
        int sw = BuildingSlot(sim, 0, "lb_second_drift");
        Assert.True(sw > 0);

        sim.Effects.BuildingCharge[sw] = 1_000_000;
        Fire(sim, 0, sw, new CellCoord(31, 22), 1);
        Assert.Empty(host.Errors);
        Assert.Equal("1", TestAccess.StateValue(host, "n"));
        Assert.Equal("0", TestAccess.StateValue(host, "p"));
        Assert.Equal("sw", TestAccess.StateValue(host, "tag"));
        Assert.Equal("31", TestAccess.StateValue(host, "x"));
        Assert.Equal("22", TestAccess.StateValue(host, "y"));
        Assert.Equal("0", TestAccess.StateValue(host, "other"));

        // The charge was spent: a second order is refused and must not fire the trigger.
        Fire(sim, 0, sw, new CellCoord(31, 22), 2);
        TestData.Run(sim, 5);
        Assert.Equal("1", TestAccess.StateValue(host, "n"));

        sim.Effects.BuildingCharge[sw] = 1_000_000;
        Fire(sim, 0, sw, new CellCoord(12, 40), 3);
        Assert.Equal("2", TestAccess.StateValue(host, "n"));
        Assert.Equal("12", TestAccess.StateValue(host, "x"));
        Assert.Empty(host.Errors);
    }

    [Fact]
    public void Superweapon_fired_trigger_is_not_called_for_an_uncharged_building_or_an_untagged_building_gets_an_empty_tag()
    {
        var (sim, host) = TestData.Tiny(ShotLua);
        int type = SimRules.IndexOf(sim.Setup.Rules.Buildings!, "lb_second_drift", b => b.Id);
        EntityId second = EntityId.None;   // setup phase: before the first Step; the first free spot wins
        for (int x = 30; x < 60 && second.IsNone; x += 4) second = sim.PlaceBuilding(0, type, new CellCoord(x, 40));
        Assert.False(second.IsNone);
        TestData.Run(sim, 3);
        int sw = -1;   // the scripted, tagged one is the other building
        for (int i = 1; i < sim.Buildings.Capacity; i++)
            if (sim.Buildings.Slots.IsIndexAlive(i) && i != second.Index) sw = i;
        Assert.True(sw > 0);
        Fire(sim, 0, sw, new CellCoord(31, 22), 1);   // not charged
        Assert.Equal("0", TestAccess.StateValue(host, "n"));

        sim.Effects.BuildingCharge[second.Index] = 1_000_000;
        Fire(sim, 0, second.Index, new CellCoord(5, 5), 2);
        Assert.Equal("1", TestAccess.StateValue(host, "n"));
        Assert.Equal("", TestAccess.StateValue(host, "tag"));
    }

    [Fact]
    public void Lb04_bonus_objective_completes_when_the_second_drift_fires_and_is_never_required()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "lb_04");
        var bot = new MissionBot(m);
        int type = SimRules.IndexOf(m.Sim.Setup.Rules.Buildings!, "lb_second_drift", b => b.Id);
        EntityId drift = m.Sim.PlaceBuilding(0, type, new CellCoord(30, 14));   // setup phase: before the first Step
        Assert.False(drift.IsNone);
        bot.Idle(5);
        MissionObjective bonus = m.Host.Objectives.Single(o => o.Id == "drift");
        Assert.False(bonus.Primary);
        Assert.Contains(m.Host.VisibleObjectives, o => o.Id == "drift");
        Assert.Equal(ObjectiveStatus.Active, bonus.Status);

        m.Sim.Effects.BuildingCharge[drift.Index] = 1_000_000;
        bot.Send(CommandKind.Special, drift, new CellCoord(60, 70), 0);
        bot.Idle(3);
        Assert.Empty(m.Host.Errors);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "drift").Status);
        Assert.Equal("true", TestAccess.StateValue(m.Host, "drift"));
        Assert.NotEqual(MissionOutcome.Won, m.Host.Outcome);
        Assert.Empty(m.Def.Locale.Warnings);
        Assert.False(m.Host.Objectives.Where(o => o.Primary).All(o => o.Status == ObjectiveStatus.Completed), "the bonus must not complete the primary objectives");
    }

    [Fact]
    public void Lb04_bonus_objective_stays_active_when_nothing_fires()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "lb_04");
        new MissionBot(m).Idle(300);
        Assert.Equal(ObjectiveStatus.Active, m.Host.Objectives.Single(o => o.Id == "drift").Status);
        Assert.Equal(5, m.Host.Objectives.Count);
        Assert.Equal(4, m.Host.VisibleObjectives.Count());
    }

    private static void Kill(MissionSession m, string typeId, int player)
    {
        int i = BuildingSlot(m.Sim, player, typeId);
        Assert.True(i > 0);
        m.Sim.Buildings.Health[i] = 0;
    }

    [Fact]
    public void Dir04_the_device_shot_wins_through_the_event()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var bot = new MissionBot(m);
        bot.Idle(3100);
        int device = BuildingSlot(m.Sim, 0, "dir_sunrise");
        m.Sim.Effects.BuildingCharge[device] = 1_000_000;
        bot.Idle(60);
        Assert.Equal("true", TestAccess.StateValue(m.Host, "charged"));
        Assert.Equal("false", TestAccess.StateValue(m.Host, "shot"));
        bot.Send(CommandKind.Special, m.Sim.Buildings.Slots.IdOf(device), new CellCoord(60, 65), 0);
        bot.Idle(5);
        Assert.Empty(m.Host.Errors);
        Assert.Equal("true", TestAccess.StateValue(m.Host, "shot"));
        Assert.Equal(MissionOutcome.Won, m.Host.Outcome);
        Assert.Equal(ObjectiveStatus.Completed, m.Host.Objectives.Single(o => o.Id == "fire").Status);
    }

    [Fact]
    public void Dir04_command_post_razed_without_the_shot_loses_even_when_charged()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var bot = new MissionBot(m);
        bot.Idle(3100);
        int device = BuildingSlot(m.Sim, 0, "dir_sunrise");
        m.Sim.Effects.BuildingCharge[device] = 1_000_000;
        bot.Idle(60);
        Kill(m, "auth_engineering", 1);
        bot.Idle(5);
        Assert.Equal(MissionOutcome.Lost, m.Host.Outcome);
    }

    [Fact]
    public void Dir04_zone_names_do_not_clash_with_actor_tags()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "dir_04");
        var tags = new HashSet<string>(m.Def.Actors.Select(a => a.Tag).Where(t => t.Length > 0));
        foreach (var z in m.Def.Zones) Assert.DoesNotContain(z.Key, tags);
        foreach (string zone in new[] { "zone_head", "zone_device", "zone_west_post" }) Assert.NotNull(m.Def.ZoneNamed(zone));
        foreach (string tag in new[] { "head", "device", "west_post" }) Assert.Contains(tag, tags);
    }

    [Fact]
    public void Lb02_gate_objective_text_asks_only_for_what_the_script_checks()
    {
        MissionSession m = MissionSession.Create(TestData.DataDir, "lb_02");
        string text = m.Def.Locale.Resolve("lb2.obj.gate");
        Assert.DoesNotContain("slinger", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Garage Rig", text);
        Assert.Contains("Lookout", text);
    }

    private static IEnumerable<int> VehicleSlots(Simulation sim, int player, string type)
    {
        int t = SimRules.IndexOf(sim.Setup.Rules.Vehicles, type, v => v.Id);
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player && sim.Units.TypeIndex[i] == t) yield return i;
    }

    [Fact]
    public void Rig_park_only_drives_to_the_cell_and_stays_packed()
    {
        const string Lua = "function OnInit() state.rig = Reinforce.Rig(0, { x = 39, y = 10 }, { x = 25, y = 10 }, true) end";
        var (sim, host, _) = World.Build(Lua, Faction.LeftBehind);
        TestData.Run(sim, 3000);
        Assert.Empty(host.Errors);
        int[] rigs = VehicleSlots(sim, 0, "lb_mother_rig_packed").ToArray();
        Assert.Single(rigs);
        CellCoord at = sim.Units.Position[rigs[0]].ToCell();
        Assert.True(Math.Abs(at.X - 25) <= 2 && Math.Abs(at.Y - 10) <= 2, $"parked at ({at.X},{at.Y})");
        Assert.Equal(-1, BuildingSlot(sim, 0, "lb_mother_rig"));
    }

    [Fact]
    public void Rig_with_park_only_false_still_unpacks_like_before()
    {
        const string Lua = "function OnInit() Reinforce.Rig(0, { x = 39, y = 10 }, { x = 25, y = 10 }, false) end";
        var (sim, host, _) = World.Build(Lua, Faction.LeftBehind);
        for (int t = 0; t < 3000 && BuildingSlot(sim, 0, "lb_mother_rig") < 0; t++) TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.True(BuildingSlot(sim, 0, "lb_mother_rig") > 0);
    }
}
