// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.Audio;

/// <summary>The events derived from constructed pairs of consecutive snapshots.</summary>
public sealed class AudioEventDeriverTests
{
    private static bool Has(List<AudioEvent> e, AudioEventKind k) => e.Any(x => x.Kind == k);

    [Fact]
    public void QuietStepMakesNoSound()
    {
        SimSnapshot a = Snap(1, new[] { Unit(1, 0, 3, 3) }, new[] { Building(1, 0, 5, 5) });
        SimSnapshot b = Snap(2, new[] { Unit(1, 0, 3, 3) }, new[] { Building(1, 0, 5, 5) });
        Assert.Empty(Derive(new AudioEventDeriver(), Frame(a, b, Hud(), Hud())));
    }

    [Fact]
    public void FirstSnapshotPairMakesNoReadyOrPlacedSounds()
    {
        SimSnapshot a = Snap(0, new[] { Unit(1, 0, 3, 3) }, new[] { Building(1, 0, 5, 5) });
        Assert.Empty(Derive(new AudioEventDeriver(), Frame(a, a, Hud(), Hud())));
    }

    [Fact]
    public void DetonationsAndDeathsGiveExplosionsBySize()
    {
        LeptonCoord at = LeptonCoord.CenterOf(new CellCoord(8, 8));
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2, fx: new[]
        {
            new FxEvent(FxKind.Detonation, at, 1),
            new FxEvent(FxKind.Detonation, at, 4),
            new FxEvent(FxKind.UnitDestroyed, at, 1),
            new FxEvent(FxKind.BuildingDestroyed, at, 3),
        });
        List<AudioEvent> e = Derive(new AudioEventDeriver(), Frame(a, b));
        Assert.Equal(2, e.Count(x => x.Kind == AudioEventKind.ExplosionSmall));
        Assert.Equal(2, e.Count(x => x.Kind == AudioEventKind.ExplosionLarge));
        Assert.All(e.Where(x => x.Kind is AudioEventKind.ExplosionSmall or AudioEventKind.ExplosionLarge), x => Assert.Equal(at, x.Pos));
    }

    [Fact]
    public void NewProjectileIsAShellAndHealthDropIsAHitAndUnderAttackForOwnUnits()
    {
        SimSnapshot a = Snap(1, new[] { Unit(1, 0, 3, 3, 100), Unit(2, 1, 9, 9, 100) });
        SimSnapshot b = Snap(2, new[] { Unit(1, 0, 3, 3, 80), Unit(2, 1, 9, 9, 70) });
        var shell = new LeptonCoord(500, 500);
        AudioFrame f = Frame(a, b) with { Projectiles = new[] { shell } };
        List<AudioEvent> e = Derive(new AudioEventDeriver(), f);
        Assert.Contains(new AudioEvent(AudioEventKind.FireShell, shell), e);
        Assert.Equal(2, e.Count(x => x.Kind == AudioEventKind.FireBullet));
        Assert.Single(e, x => x.Kind == AudioEventKind.UnderAttack);
    }

    [Fact]
    public void EnemyDamageAloneIsNotUnderAttackAndSplashDamageIsNotABullet()
    {
        SimSnapshot a = Snap(1, new[] { Unit(2, 1, 9, 9, 100) });
        SimSnapshot b = Snap(2, new[] { Unit(2, 1, 9, 9, 70) },
            fx: new[] { new FxEvent(FxKind.Detonation, LeptonCoord.CenterOf(new CellCoord(9, 10)), 1) });
        List<AudioEvent> e = Derive(new AudioEventDeriver(), Frame(a, b));
        Assert.False(Has(e, AudioEventKind.UnderAttack));
        Assert.False(Has(e, AudioEventKind.FireBullet));
    }

    [Fact]
    public void OwnNewUnitIsReadyAndOwnNewBuildingIsPlaced_EnemyOnesAreNot()
    {
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2, new[] { Unit(1, 0, 3, 3), Unit(2, 1, 9, 9) }, new[] { Building(1, 0, 5, 5), Building(2, 1, 12, 12) });
        List<AudioEvent> e = Derive(new AudioEventDeriver(), Frame(a, b));
        Assert.Single(e, x => x.Kind == AudioEventKind.UnitReady);
        Assert.Single(e, x => x.Kind == AudioEventKind.BuildingPlaced);
    }

    [Fact]
    public void BuildingGoneWithoutDestructionIsSold_ButDestroyedIsNot()
    {
        SimSnapshot a = Snap(1, buildings: new[] { Building(1, 0, 5, 5) });
        List<AudioEvent> sold = Derive(new AudioEventDeriver(), Frame(a, Snap(2)));
        Assert.True(Has(sold, AudioEventKind.Sold));

        SimSnapshot dead = Snap(2, fx: new[] { new FxEvent(FxKind.BuildingDestroyed, LeptonCoord.CenterOf(new CellCoord(6, 6)), 2) });
        List<AudioEvent> destroyed = Derive(new AudioEventDeriver(), Frame(a, dead));
        Assert.False(Has(destroyed, AudioEventKind.Sold));
        Assert.True(Has(destroyed, AudioEventKind.ExplosionLarge));
        Assert.True(Has(destroyed, AudioEventKind.UnderAttack));
    }

    [Fact]
    public void HudChangesGivePowerFundsConstructionUpgradeAndUnload()
    {
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2);
        var d = new AudioEventDeriver();

        Assert.True(Has(Derive(d, Frame(a, b, Hud(), Hud(powerOut: 10, powerUse: 40))), AudioEventKind.LowPower));
        Assert.False(Has(Derive(d, Frame(a, b, Hud(), Hud())), AudioEventKind.LowPower));
        Assert.True(Has(Derive(d, Frame(a, b, Hud(), Hud(stalled: true))), AudioEventKind.LowFunds));
        Assert.True(Has(Derive(d, Frame(a, b, Hud(), Hud(readyStructure: 2))), AudioEventKind.ConstructionComplete));
        Assert.False(Has(Derive(d, Frame(a, b, Hud(readyStructure: 2), Hud(readyStructure: 2))), AudioEventKind.ConstructionComplete));
        Assert.True(Has(Derive(d, Frame(a, b, Hud(upgrades: 1), Hud(upgrades: 3))), AudioEventKind.UpgradeComplete));
        Assert.True(Has(Derive(d, Frame(a, b, Hud(cash: 1000), Hud(cash: 1025))), AudioEventKind.HarvesterUnload));
        Assert.False(Has(Derive(d, Frame(a, b, Hud(cash: 1000), Hud(cash: 990))), AudioEventKind.HarvesterUnload));
    }

    [Fact]
    public void GoingUnderground_Telegraph_Radiation_And_Blackout()
    {
        var d = new AudioEventDeriver();
        SimSnapshot a = Snap(1, new[] { Unit(1, 0, 3, 3), Unit(2, 1, 9, 9) });
        SimSnapshot b = Snap(2, new[] { Unit(1, 0, 3, 3), Unit(2, 1, 9, 9, underground: true) },
            telegraphs: new[] { new TelegraphView(new CellCoord(4, 4), 1, 40, TelegraphKind.Unit) });
        List<AudioEvent> e = Derive(d, Frame(a, b));
        Assert.Single(e, x => x.Kind == AudioEventKind.TunnelEnter);
        Assert.Single(e, x => x.Kind == AudioEventKind.SurfaceRumble);

        // The same telegraph one tick later is not new and is not on a repeat boundary.
        SimSnapshot c = Snap(3, b.Units.ToArray(), telegraphs: new[] { new TelegraphView(new CellCoord(4, 4), 1, 39, TelegraphKind.Unit) });
        Assert.False(Has(Derive(d, Frame(b, c)), AudioEventKind.SurfaceRumble));

        var ground = new byte[16 * 16];
        ground[(3 * 16) + 3] = 0x05 | GroundLayers.Blackout;
        SimSnapshot g = Snap(4, new[] { Unit(1, 0, 3, 3) }, ground: ground);
        List<AudioEvent> gas = Derive(d, Frame(Snap(3), g));
        Assert.Single(gas, x => x.Kind == AudioEventKind.RadiationCrackle);
        Assert.Single(gas, x => x.Kind == AudioEventKind.BlackoutHum);
    }

    [Fact]
    public void OwnQueuedOrderIsAcknowledgedOnce()
    {
        var d = new AudioEventDeriver();
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2);
        var move = new Command(5, 0, 7, CommandKind.Move, new EntityId(1, 1), EntityId.None, new CellCoord(2, 2), 0);
        var enemyMove = move with { Player = 1, Sequence = 8 };
        AudioFrame f = Frame(a, b) with { Pending = new[] { move, enemyMove } };
        Assert.Single(Derive(d, f), x => x.Kind == AudioEventKind.MoveAck);
        Assert.Empty(Derive(d, Frame(b, Snap(3)) with { Pending = new[] { move } }));
    }
}
