// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Ui.Alerts;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S47Qol;

/// <summary>S47 U-13 (cnc#33): the "Enemy spotted" alert.</summary>
public sealed class S47QolEnemySpottedTests
{
    private const int L = SimConstants.LeptonsPerCell;

    private static SimSnapshot Snap(int tick, UnitView[]? units = null, BuildingView[]? buildings = null) =>
        new(tick, 128, 128, units ?? Array.Empty<UnitView>(), Array.Empty<Rts.Sim.Rules.LandType>(), 0UL, buildings ?? Array.Empty<BuildingView>(), 0,
            Array.Empty<byte>(), null);

    private static UnitView Enemy(ushort id, int cellX, int cellY) =>
        new(new EntityId(id, 0), UnitKind.Vehicle, 1, 3, new LeptonCoord(cellX * L, cellY * L), 0, 100);

    private static readonly BuildingView Own = new(new EntityId(1, 0), 0, 1, new CellCoord(20, 20), 2, 2, 100, false);

    private static List<GameAlert> Derive(SimSnapshot a, SimSnapshot b, bool enemyKnown = true)
    {
        var alerts = new List<GameAlert>();
        new AlertDeriver(null, enemyKnown ? p => p == 1 : null).Derive(new AudioFrame(a, b, 0), Array.Empty<AudioEvent>(), alerts);
        return alerts;
    }

    [Fact]
    public void An_enemy_coming_into_view_near_a_building_raises_the_alert_at_its_place()
    {
        var alerts = Derive(Snap(1, buildings: new[] { Own }), Snap(2, new[] { Enemy(7, 30, 20) }, new[] { Own }));
        GameAlert a = Assert.Single(alerts);
        Assert.Equal(AlertKind.EnemySpotted, a.Kind);
        Assert.Equal(new LeptonCoord(30 * L, 20 * L), a.Pos);
    }

    [Fact]
    public void An_enemy_far_away_or_already_in_view_or_without_hostility_raises_nothing()
    {
        Assert.Empty(Derive(Snap(1, buildings: new[] { Own }), Snap(2, new[] { Enemy(7, 90, 90) }, new[] { Own })));
        Assert.Empty(Derive(Snap(1, new[] { Enemy(7, 30, 20) }, new[] { Own }), Snap(2, new[] { Enemy(7, 31, 20) }, new[] { Own })));
        Assert.Empty(Derive(Snap(1, buildings: new[] { Own }), Snap(2, new[] { Enemy(7, 30, 20) }, new[] { Own }), enemyKnown: false));
    }

    [Fact]
    public void The_alert_is_a_warning_with_its_own_text_in_the_attack_category_and_a_slow_repeat()
    {
        Assert.Equal("Enemy spotted", AlertInfo.Text(AlertKind.EnemySpotted));
        Assert.Equal(AlertTone.Warning, AlertInfo.Tone(AlertKind.EnemySpotted));
        Assert.Equal(AlertCategory.Attack, AlertMix.CategoryOf(AlertKind.EnemySpotted));
        Assert.True(AlertInfo.IntervalTicks(AlertKind.EnemySpotted) >= 10 * AlertInfo.TicksPerSecond);
    }
}
