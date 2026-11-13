// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using static Rts.Game.Tests.Audio.AudioRulesFixture;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.Audio;

/// <summary>S22: faction, unit-class and weapon-class keys on the derived events, and the new naval, air and rig events, with the real rules.</summary>
public sealed class AudioDeriverKeysTests
{
    private static AudioEvent Only(List<AudioEvent> e, AudioEventKind kind) => Assert.Single(e, x => x.Kind == kind);

    private static SimSnapshot Water(int tick, UnitView[]? units = null, FxEvent[]? fx = null, BuildingView[]? buildings = null)
    {
        var land = new LandType[16 * 16];
        Array.Fill(land, LandType.Water);
        return new SimSnapshot(tick, 16, 16, units ?? Array.Empty<UnitView>(), land, 0UL, buildings ?? Array.Empty<BuildingView>(), 0, Array.Empty<byte>(), fx);
    }

    [Theory]
    [InlineData("dir_conscript", true, "infantry", "directorate")]
    [InlineData("auth_rifleman", true, "infantry", "authority")]
    [InlineData("lb_drifter_brute", true, "infantry", "left_behind")]
    [InlineData("auth_jeep", false, "vehicle", "authority")]
    [InlineData("exp_destroyer", false, "ship", "expedition")]
    [InlineData("exp_jet", false, "air", "expedition")]
    [InlineData("lb_rotor", false, "air", "left_behind")]
    public void AUnitDeathCarriesItsClassAndFaction(string typeId, bool infantry, string cls, string faction)
    {
        UnitView dead = infantry ? Soldier(1, 1, typeId, 6, 6) : Vehicle(1, 1, typeId, 6, 6);
        SimSnapshot a = Snap(1, new[] { dead });
        SimSnapshot b = Snap(2, fx: new[] { new FxEvent(FxKind.UnitDestroyed, dead.Position, 1) });
        AudioEvent e = Only(Derive(a, b), AudioEventKind.UnitDied);
        Assert.Equal(cls, e.Class);
        Assert.Equal(faction, e.Faction);
        Assert.Equal(dead.Position, e.Pos);
        Assert.DoesNotContain(Derive(a, b), x => x.Kind == AudioEventKind.ExplosionSmall);
    }

    [Fact]
    public void ADeathPicksTheUnitThatVanishedNotOneThatStillStandsNearby()
    {
        UnitView dead = Soldier(1, 1, "dir_conscript", 6, 6);
        UnitView alive = Vehicle(2, 1, "auth_jeep", 6, 6);
        SimSnapshot a = Snap(1, new[] { dead, alive });
        SimSnapshot b = Snap(2, new[] { alive }, fx: new[] { new FxEvent(FxKind.UnitDestroyed, dead.Position, 1) });
        Assert.Equal("infantry", Only(Derive(a, b), AudioEventKind.UnitDied).Class);
    }

    [Fact]
    public void ADestroyedBuildingCarriesItsFaction()
    {
        BuildingView bld = Building(1, 1, "dir_hatch", 4, 4);
        SimSnapshot a = Snap(1, buildings: new[] { bld });
        SimSnapshot b = Snap(2, fx: new[] { new FxEvent(FxKind.BuildingDestroyed, new LeptonCoord(5 * 256, 5 * 256), 2) });
        AudioEvent e = Only(Derive(a, b), AudioEventKind.BuildingDestroyed);
        Assert.Equal(AudioRules.FactionKey(Rules.Buildings[bld.TypeIndex].Faction), e.Faction);
        Assert.DoesNotContain(Derive(a, b), x => x.Kind == AudioEventKind.ExplosionLarge);
    }

    [Fact]
    public void WithoutRulesTheOldEventsAreRaised()
    {
        LeptonCoord at = Cell(8, 8);
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2, fx: new[] { new FxEvent(FxKind.UnitDestroyed, at, 1), new FxEvent(FxKind.BuildingDestroyed, at, 3) });
        List<AudioEvent> e = Derive(new AudioEventDeriver(), Frame(a, b));
        Assert.Contains(new AudioEvent(AudioEventKind.ExplosionSmall, at), e);
        Assert.Contains(new AudioEvent(AudioEventKind.ExplosionLarge, at), e);
    }

    [Fact]
    public void AProjectileIsNamedByItsWeapon()
    {
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2);
        var shells = new[] { Cell(3, 3), Cell(4, 4), Cell(5, 5) };
        int[] weapons = { Wpn("auth_tank_cannon"), Wpn("auth_rocket"), Wpn("exp_cruiser_gun") };
        List<AudioEvent> e = Derive(a, b, f => f with { Projectiles = shells, ProjectileWeapons = weapons });
        Assert.Contains(new AudioEvent(AudioEventKind.FireShell, shells[0], "cannon", "authority"), e);
        Assert.Contains(new AudioEvent(AudioEventKind.FireMissile, shells[1], "rocket", "authority"), e);
        Assert.Contains(new AudioEvent(AudioEventKind.FireShell, shells[2], "naval_heavy", "expedition"), e);
    }

    [Fact]
    public void AProjectileWithoutWeaponInfoStaysAGenericShell()
    {
        var shell = Cell(3, 3);
        List<AudioEvent> e = Derive(Snap(1), Snap(2), f => f with { Projectiles = new[] { shell } });
        Assert.Contains(new AudioEvent(AudioEventKind.FireShell, shell), e);
    }

    [Fact]
    public void ASilentWeaponRaisesNoShotEvent()
    {
        var shell = Cell(3, 3);
        List<AudioEvent> e = Derive(Snap(1), Snap(2), f => f with { Projectiles = new[] { shell }, ProjectileWeapons = new[] { Wpn("dir_meltdown") } });
        Assert.DoesNotContain(e, x => x.Kind is AudioEventKind.FireShell or AudioEventKind.FireMissile or AudioEventKind.FireBullet);
    }

    [Fact]
    public void AnInstantHitIsNamedByTheNearestEnemyShooterInRange()
    {
        UnitView shooter = Soldier(1, 1, "auth_marksman", 2, 8);
        UnitView farShooter = Soldier(2, 1, "dir_conscript", 14, 14);
        UnitView victim = Vehicle(3, 0, "auth_jeep", 6, 8, 100);
        SimSnapshot a = Snap(1, new[] { shooter, farShooter, victim });
        SimSnapshot b = Snap(2, new[] { shooter, farShooter, victim with { Health = 80 } });
        AudioEvent hit = Only(Derive(a, b), AudioEventKind.FireBullet);
        Assert.Equal("sniper", hit.Class);
        Assert.Equal("authority", hit.Faction);
        Assert.Equal(victim.Position, hit.Pos);
    }

    [Fact]
    public void AnInstantHitWithNoShooterInViewIsAPlainFireBullet()
    {
        UnitView victim = Vehicle(3, 0, "auth_jeep", 6, 8, 100);
        SimSnapshot a = Snap(1, new[] { victim });
        SimSnapshot b = Snap(2, new[] { victim with { Health = 80 } });
        Assert.Contains(new AudioEvent(AudioEventKind.FireBullet, victim.Position), Derive(a, b));
    }

    [Fact]
    public void AFriendlyUnitIsNotTheShooter()
    {
        UnitView friend = Soldier(1, 0, "auth_marksman", 2, 8);
        UnitView victim = Vehicle(3, 0, "auth_jeep", 6, 8, 100);
        SimSnapshot a = Snap(1, new[] { friend, victim });
        SimSnapshot b = Snap(2, new[] { friend, victim with { Health = 80 } });
        Assert.Contains(new AudioEvent(AudioEventKind.FireBullet, victim.Position), Derive(a, b));
    }

    [Fact]
    public void ManyHitsInOneStepStayCheap()
    {
        var before = new List<UnitView>();
        var after = new List<UnitView>();
        for (int i = 0; i < 60; i++)
        {
            UnitView v = Vehicle(i + 1, (byte)(i % 2), "auth_jeep", 2 + (i % 12), 2 + (i / 12), 100);
            before.Add(v);
            after.Add(v with { Health = 90 });
        }

        List<AudioEvent> e = Derive(Snap(1, before.ToArray()), Snap(2, after.ToArray()));
        Assert.Equal(60, e.Count(x => x.Kind == AudioEventKind.FireBullet));   // one per victim, as before; the director's cooldown and cap merge them
    }

    [Fact]
    public void AMissIntoOpenWaterIsAWaterImpact_AHitOnAShipIsAnExplosion()
    {
        LeptonCoord at = Cell(8, 8);
        SimSnapshot a = Water(1);
        SimSnapshot b = Water(2, fx: new[] { new FxEvent(FxKind.Detonation, at, 1), new FxEvent(FxKind.Detonation, Cell(2, 2), 4) });
        List<AudioEvent> e = Derive(a, b);
        Assert.Contains(new AudioEvent(AudioEventKind.WaterImpact, at), e);
        Assert.Contains(new AudioEvent(AudioEventKind.WaterImpact, Cell(2, 2), "large"), e);
        Assert.DoesNotContain(e, x => x.Kind is AudioEventKind.ExplosionSmall or AudioEventKind.ExplosionLarge);

        UnitView ship = Vehicle(1, 1, "exp_destroyer", 8, 8);
        List<AudioEvent> hit = Derive(Water(1, new[] { ship }), Water(2, new[] { ship }, fx: new[] { new FxEvent(FxKind.Detonation, at, 1) }));
        Assert.Contains(new AudioEvent(AudioEventKind.ExplosionSmall, at), hit);
        Assert.DoesNotContain(hit, x => x.Kind == AudioEventKind.WaterImpact);
    }

    [Fact]
    public void ADetonationOnLandIsAnExplosionAsBefore()
    {
        LeptonCoord at = Cell(8, 8);
        List<AudioEvent> e = Derive(Snap(1), Snap(2, fx: new[] { new FxEvent(FxKind.Detonation, at, 1) }));
        Assert.Contains(new AudioEvent(AudioEventKind.ExplosionSmall, at), e);
    }

    [Fact]
    public void ABlastBelowABomberIsABombExplosion()
    {
        LeptonCoord at = Cell(8, 8);
        UnitView bomber = Vehicle(1, 1, "auth_bomber", 8, 7);
        List<AudioEvent> e = Derive(Snap(1, new[] { bomber }), Snap(2, new[] { bomber }, fx: new[] { new FxEvent(FxKind.Detonation, at, 2) }));
        Assert.Contains(new AudioEvent(AudioEventKind.ExplosionLarge, at, "bomb"), e);
    }

    [Fact]
    public void ANewEnemySubmarineIsASonarContact_OwnAndSurfaceShipsAreNot()
    {
        UnitView sub = Vehicle(1, 1, "exp_missile_sub", 5, 5);
        List<AudioEvent> e = Derive(Snap(1), Snap(2, new[] { sub }));
        Assert.Equal(sub.Position, Only(e, AudioEventKind.Sonar).Pos);

        Assert.DoesNotContain(Derive(Snap(1), Snap(2, new[] { sub with { Player = 0 } })), x => x.Kind == AudioEventKind.Sonar);
        Assert.DoesNotContain(Derive(Snap(1), Snap(2, new[] { Vehicle(2, 1, "exp_destroyer", 5, 5) })), x => x.Kind == AudioEventKind.Sonar);
        Assert.DoesNotContain(Derive(Snap(1, new[] { sub }), Snap(2, new[] { sub })), x => x.Kind == AudioEventKind.Sonar);   // already known
    }

    [Fact]
    public void ANewOwnShipRaisesTheHornAndNotUnitReady_ANewTankStillRaisesUnitReady()
    {
        PlayerHud hud = Hud() with { Faction = Faction.Expedition };
        List<AudioEvent> ship = Derive(Snap(1), Snap(2, new[] { Vehicle(1, 0, "exp_destroyer", 5, 5) }), hud: hud);
        AudioEvent horn = Only(ship, AudioEventKind.ShipHorn);
        Assert.Equal("ship", horn.Class);
        Assert.Equal("expedition", horn.Faction);
        Assert.DoesNotContain(ship, x => x.Kind == AudioEventKind.UnitReady);

        List<AudioEvent> tank = Derive(Snap(1), Snap(2, new[] { Vehicle(1, 0, "auth_jeep", 5, 5) }), hud: Hud() with { Faction = Faction.Authority });
        AudioEvent ready = Only(tank, AudioEventKind.UnitReady);
        Assert.Equal("vehicle", ready.Class);
        Assert.Equal("authority", ready.Faction);
    }

    [Fact]
    public void PlacedAndCompleteCarryTheFaction()
    {
        BuildingView bld = Building(1, 0, "dir_hatch", 5, 5);
        List<AudioEvent> placed = Derive(Snap(1), Snap(2, buildings: new[] { bld }), hud: Hud());
        Assert.Equal(AudioRules.FactionKey(Rules.Buildings[bld.TypeIndex].Faction) ?? "authority", Only(placed, AudioEventKind.BuildingPlaced).Faction);

        PlayerHud before = Hud(readyStructure: -1) with { Faction = Faction.Directorate };
        PlayerHud after = Hud(readyStructure: 0) with { Faction = Faction.Directorate };
        List<AudioEvent> done = Derive(Snap(1), Snap(2), hud: null, edit: f => f with { PreviousHud = before, CurrentHud = after });
        Assert.Equal("directorate", Only(done, AudioEventKind.ConstructionComplete).Faction);
    }

    [Fact]
    public void ARigUnfoldingIsADeployNotAPlacementOrAReadyUnit_AndPackingIsNotASale()
    {
        int packed = Veh("lb_mother_rig_packed");
        int rigBuilding = Bld("lb_mother_rig");
        Assert.Equal(rigBuilding, Rules.Vehicles[packed].UnpacksInto);
        UnitView truck = new(new EntityId(1, 1), UnitKind.Vehicle, 0, packed, Cell(6, 6), 0, 100);
        BuildingView rig = new(new EntityId(2, 1), 0, rigBuilding, new CellCoord(5, 5), 3, 3, 100, false);
        PlayerHud hud = Hud();

        List<AudioEvent> unfold = Derive(Snap(1, new[] { truck }), Snap(2, buildings: new[] { rig }), hud: hud);
        Assert.Equal("left_behind", Only(unfold, AudioEventKind.RigDeploy).Faction);
        Assert.DoesNotContain(unfold, x => x.Kind == AudioEventKind.BuildingPlaced);

        UnitView truck2 = truck with { Id = new EntityId(3, 1) };
        List<AudioEvent> pack = Derive(Snap(1, buildings: new[] { rig }), Snap(2, new[] { truck2 }), hud: hud);
        Assert.Single(pack, x => x.Kind == AudioEventKind.RigPack);
        Assert.DoesNotContain(pack, x => x.Kind is AudioEventKind.Sold or AudioEventKind.UnitReady);
    }

    [Fact]
    public void AnAircraftSoundsOnItsOwnTimer_JetsAsFlyby_RotorsAsHover_AirshipsNever()
    {
        var d = new AudioEventDeriver();
        UnitView jet = Vehicle(1, 1, "exp_jet", 5, 5);
        UnitView rotor = Vehicle(2, 1, "lb_rotor", 7, 7);
        UnitView blimp = Vehicle(3, 1, "dir_dirigible", 9, 9);
        var kinds = new List<(int Tick, AudioEventKind Kind)>();
        int tps = SimConstants.TicksPerSecond;
        for (int tick = 1; tick <= tps * 20; tick++)
        {
            UnitView[] units = { jet with { Position = new LeptonCoord(jet.Position.X + (tick * 40), jet.Position.Y) }, rotor, blimp };
            UnitView[] before = { jet with { Position = new LeptonCoord(jet.Position.X + ((tick - 1) * 40), jet.Position.Y) }, rotor, blimp };
            foreach (AudioEvent e in Derive(d, Snap(tick - 1, before), Snap(tick, units)))
            {
                if (e.Kind is AudioEventKind.AirFlyby or AudioEventKind.AirHover) kinds.Add((tick, e.Kind));
            }
        }

        int flybys = kinds.Count(k => k.Kind == AudioEventKind.AirFlyby);
        int hovers = kinds.Count(k => k.Kind == AudioEventKind.AirHover);
        Assert.InRange(flybys, 2, 4);   // about one per 6 s over 20 s
        Assert.InRange(hovers, 3, 5);   // about one per 4 s
        Assert.True(kinds.Zip(kinds.Skip(1)).Where(p => p.First.Kind == p.Second.Kind).All(p => p.Second.Tick - p.First.Tick >= 4 * tps));
    }

    [Fact]
    public void AParkedJetMakesNoFlyby()
    {
        var d = new AudioEventDeriver();
        UnitView jet = Vehicle(1, 1, "exp_jet", 5, 5);
        var count = 0;
        for (int tick = 1; tick <= SimConstants.TicksPerSecond * 20; tick++) count += Derive(d, Snap(tick - 1, new[] { jet }), Snap(tick, new[] { jet })).Count(e => e.Kind == AudioEventKind.AirFlyby);
        Assert.Equal(0, count);
    }
}
