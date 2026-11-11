// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Audio;
using Rts.Game.Controls;
using Rts.Game.Ui;
using Rts.Game.Ui.Alerts;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.Game.Tests.S19;

/// <summary>M13-15 item 2 (D-205): idle harvester / factory finder, the cycling, and the new alerts with their rate limit.</summary>
public sealed class IdleAlertTests
{
    private static readonly RuleSet Rules = RulesLoader.Load(RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("rules"));
    private static SimRules Sim => Rules.Sim;

    private static int HarvesterType => Enumerable.Range(0, Sim.Vehicles.Count).First(i => Sim.Vehicles[i].HarvestCapacity > 0);
    private static int TankType => Enumerable.Range(0, Sim.Vehicles.Count).First(i => Sim.Vehicles[i].HarvestCapacity == 0);
    private static int Factory(ProductionCategory c) => Enumerable.Range(0, Sim.Buildings.Count).First(i => Sim.Buildings[i].Produces == c);

    private static ViewActor Veh(int slot, int type, bool idle) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Vehicle, 0, Relation.Self, type, new CellCoord(slot, 2), new LeptonCoord(slot * 256, 512), 100, 100,
            idle ? MissionKind.Guard : MissionKind.Move, idle, EntityId.None, false, 1, 1);

    private static ViewActor Bld(int slot, int type) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Building, 0, Relation.Self, type, new CellCoord(slot, 5), new LeptonCoord(slot * 256, 1280), 500, 500,
            MissionKind.None, false, EntityId.None, false, 2, 2);

    private static PlayerHud Hud(params QueueView[] queues) =>
        new(1000, 0, 5000, 100, 50, queues, Array.Empty<(ProductionCategory, int)>(), false);

    private static QueueView Busy(ProductionCategory c) => new(c, new[] { new QueueItemView(0, 100, false) }, -1);

    [Fact]
    public void Only_idle_harvesters_are_found_not_tanks_or_busy_harvesters()
    {
        var own = new List<ViewActor> { Veh(1, HarvesterType, true), Veh(2, HarvesterType, false), Veh(3, TankType, true), Veh(4, HarvesterType, true) };
        var idle = new List<ViewActor>();
        IdleFinder.Harvesters(own, Sim, idle);
        Assert.Equal(new[] { 1, 4 }, idle.Select(a => (int)a.Id.Index));
    }

    [Fact]
    public void The_key_cycles_through_the_idle_harvesters_in_slot_order_and_wraps()
    {
        var idle = new List<ViewActor> { Veh(1, HarvesterType, true), Veh(4, HarvesterType, true), Veh(7, HarvesterType, true) };
        EntityId last = default;
        var visited = new List<int>();
        for (int n = 0; n < 4; n++)
        {
            ViewActor next = IdleFinder.Next(idle, last)!.Value;
            visited.Add(next.Id.Index);
            last = next.Id;
        }

        Assert.Equal(new[] { 1, 4, 7, 1 }, visited);
        Assert.Null(IdleFinder.Next(new List<ViewActor>(), last));
    }

    [Fact]
    public void A_factory_is_idle_when_its_lane_is_empty_and_busy_lanes_are_not_counted()
    {
        int barracks = Factory(ProductionCategory.Infantry), vehicles = Factory(ProductionCategory.Vehicle);
        var own = new List<ViewActor> { Bld(1, barracks), Bld(2, vehicles) };
        var idle = new List<ViewActor>();
        IdleFinder.Factories(own, Sim, Hud(Busy(ProductionCategory.Vehicle)), idle);
        Assert.Equal(new[] { 1 }, idle.Select(a => (int)a.Id.Index));   // infantry lane empty, vehicle lane busy
    }

    [Fact]
    public void Structure_producers_are_never_idle_factories()
    {
        int yard = Enumerable.Range(0, Sim.Buildings.Count).First(i => Sim.Buildings[i].Produces is null or ProductionCategory.Structure or ProductionCategory.Upgrade);
        var idle = new List<ViewActor>();
        IdleFinder.Factories(new List<ViewActor> { Bld(1, yard) }, Sim, Hud(), idle);
        Assert.Empty(idle);
    }

    [Fact]
    public void An_idle_harvester_alerts_once_after_fifteen_seconds_and_a_busy_one_never()
    {
        var watch = new IdleWatch();
        var alerts = new List<GameAlert>();
        var own = new List<ViewActor> { Veh(1, HarvesterType, true), Veh(2, HarvesterType, false) };
        for (int t = 0; t < IdleWatch.HarvesterTicks; t++) watch.Observe(t, own, Sim, Hud(), alerts);
        Assert.Empty(alerts);
        watch.Observe(IdleWatch.HarvesterTicks, own, Sim, Hud(), alerts);
        watch.Observe(IdleWatch.HarvesterTicks + 1, own, Sim, Hud(), alerts);
        GameAlert a = Assert.Single(alerts);
        Assert.Equal(AlertKind.HarvesterIdle, a.Kind);
        Assert.Equal(256, a.Pos!.Value.X);
    }

    [Fact]
    public void Production_idle_needs_a_busy_lane_that_then_stands_empty_with_a_factory()
    {
        int barracks = Factory(ProductionCategory.Infantry);
        var own = new List<ViewActor> { Bld(1, barracks) };
        var watch = new IdleWatch();
        var alerts = new List<GameAlert>();
        for (int t = 0; t < 400; t++) watch.Observe(t, own, Sim, Hud(), alerts);   // never busy: no alert
        Assert.Empty(alerts);
        watch.Observe(400, own, Sim, Hud(Busy(ProductionCategory.Infantry)), alerts);
        for (int t = 401; t < 401 + IdleWatch.ProductionTicks; t++) watch.Observe(t, own, Sim, Hud(), alerts);
        Assert.Empty(alerts);
        watch.Observe(401 + IdleWatch.ProductionTicks, own, Sim, Hud(), alerts);
        Assert.Equal(AlertKind.ProductionIdle, Assert.Single(alerts).Kind);
    }

    [Fact]
    public void The_new_alerts_are_rate_limited_to_one_every_thirty_seconds()
    {
        var log = new AlertLog();
        Assert.True(log.Add(new GameAlert(AlertKind.ProductionIdle, 0)));
        Assert.False(log.Add(new GameAlert(AlertKind.ProductionIdle, 60)));
        Assert.Equal("Production idle", log.Lines(60)[0].Text);
        Assert.Equal(30 * AlertInfo.TicksPerSecond, AlertInfo.IntervalTicks(AlertKind.HarvesterIdle));
        Assert.Equal(AlertTone.Warning, AlertInfo.Tone(AlertKind.ProductionBlocked));
    }

    [Fact]
    public void The_deriver_turns_the_existing_upgrade_and_low_funds_sounds_into_alerts()
    {
        var snap = new SimSnapshot(1, 8, 8, Array.Empty<UnitView>(), Array.Empty<LandType>(), 0UL, Array.Empty<BuildingView>(), 0, Array.Empty<byte>());
        var deriver = new AlertDeriver();
        var alerts = new List<GameAlert>();
        var frame = new AudioFrame(snap, snap, 0);
        deriver.Derive(frame, new[] { new AudioEvent(AudioEventKind.UpgradeComplete), new AudioEvent(AudioEventKind.LowFunds) }, alerts);
        Assert.Contains(alerts, a => a.Kind == AlertKind.UpgradeComplete);
        Assert.Contains(alerts, a => a.Kind == AlertKind.ProductionBlocked);
    }

    [Fact]
    public void The_idle_keys_have_defaults_that_do_not_clash()
    {
        Assert.Empty(KeyBindings.Default.Conflicts());
        Assert.NotEqual(KeyBindings.Default.Get(GameAction.IdleHarvester), KeyBindings.Default.Get(GameAction.IdleFactory));
        Assert.Equal("Harvester idle", UiText.Get(UiText.AlertHarvesterIdle));
    }
}
