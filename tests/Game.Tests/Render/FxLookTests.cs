// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Tests.Render;

/// <summary>D-184: the pure logic of the renderer look improvements (damage tiers, remains, dust, mood) and their placeholder art.</summary>
[Collection("UiStatics")]   // S18 TESTS-REPO-01: touches process-wide statics, runs alone
public sealed class FxLookTests
{
    private static readonly SimRules Rules = LoadRules();

    private static SimRules LoadRules()
    {
        string dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        return RulesLoader.Load(dir).Sim;
    }

    private static int Veh(string id) => Enumerable.Range(0, Rules.Vehicles.Count).First(i => Rules.Vehicles[i].Id == id);

    private static int Bld(string id) => Enumerable.Range(0, Rules.Buildings.Count).First(i => Rules.Buildings[i].Id == id);

    private static LeptonCoord Cell(double x, double y) => new((int)(x * 256), (int)(y * 256));

    private static SimSnapshot Snap(int tick, IEnumerable<UnitView>? units = null, IEnumerable<BuildingView>? buildings = null, IEnumerable<FxEvent>? fx = null, int size = 16, LandType land = LandType.Clear)
    {
        var l = new LandType[size * size];
        Array.Fill(l, land);
        return new SimSnapshot(tick, size, size, (units ?? Array.Empty<UnitView>()).ToList(), l, 0, (buildings ?? Array.Empty<BuildingView>()).ToList(), -1, Array.Empty<byte>(), (fx ?? Array.Empty<FxEvent>()).ToList());
    }

    private static UnitView Tank(int id, double x, double y, double hp = 1.0, string type = "auth_tank", byte facing = 64)
    {
        int t = Veh(type);
        return new UnitView(new EntityId((ushort)id, 1), UnitKind.Vehicle, 0, t, Cell(x, y), facing, (int)(Rules.Vehicles[t].MaxHealth * hp));
    }

    private static BuildingView Barracks(int id, int x, int y, double hp = 1.0, string type = "auth_barracks", bool ghost = false)
    {
        int t = Bld(type);
        BuildingType bt = Rules.Buildings[t];
        return new BuildingView(new EntityId((ushort)id, 1), 0, t, new CellCoord(x, y), bt.FootprintW, bt.FootprintH, (int)(bt.MaxHealth * hp), ghost);
    }

    // ---- damage tiers ------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(100, 100, DamageTier.None)]
    [InlineData(50, 100, DamageTier.None)]       // exactly half is still clean (same rule as the damaged frame)
    [InlineData(49, 100, DamageTier.Smoke)]
    [InlineData(25, 100, DamageTier.Smoke)]      // exactly a quarter still only smokes
    [InlineData(24, 100, DamageTier.Fire)]
    [InlineData(1, 100, DamageTier.Fire)]
    [InlineData(0, 100, DamageTier.Fire)]
    [InlineData(450, 450, DamageTier.None)]
    [InlineData(224, 450, DamageTier.Smoke)]
    [InlineData(112, 450, DamageTier.Fire)]
    [InlineData(10, 0, DamageTier.None)]         // unknown maximum
    public void TierThresholds(int health, int max, DamageTier expected) => Assert.Equal(expected, DamageTiers.For(health, max));

    [Fact]
    public void SmokeThresholdMatchesTheBuildingDamagedFrame()
    {
        for (int max = 1; max <= 60; max++)
            for (int h = 0; h <= max; h++)
                Assert.Equal(h * SpriteStates.DamagedDivisor < max, DamageTiers.For(h, max) != DamageTier.None);
    }

    [Fact]
    public void TrackerBurstsWhenAHigherTierIsEntered()
    {
        var tracker = new DamageTierTracker();
        var changes = new List<TierChange>();
        tracker.Observe(Snap(10, new[] { Tank(1, 3, 3) }, new[] { Barracks(2, 5, 5) }), Rules, changes);
        Assert.Empty(changes);
        tracker.Observe(Snap(11, new[] { Tank(1, 3, 3, 0.45) }, new[] { Barracks(2, 5, 5, 0.9) }), Rules, changes);
        Assert.Equal(new[] { new TierChange(false, 0, DamageTier.None, DamageTier.Smoke) }, changes);
        tracker.Observe(Snap(12, new[] { Tank(1, 3, 3, 0.45) }, new[] { Barracks(2, 5, 5, 0.2) }), Rules, changes);
        Assert.Equal(new[] { new TierChange(true, 0, DamageTier.None, DamageTier.Fire) }, changes);   // a building jumping two tiers is one burst of the tier it reached
        tracker.Observe(Snap(13, new[] { Tank(1, 3, 3, 0.1) }, new[] { Barracks(2, 5, 5, 0.2) }), Rules, changes);
        Assert.Equal(new[] { new TierChange(false, 0, DamageTier.Smoke, DamageTier.Fire) }, changes);
    }

    [Fact]
    public void TrackerIsQuietForFirstSightRepairsRepeatsAndReloads()
    {
        var tracker = new DamageTierTracker();
        var changes = new List<TierChange>();
        tracker.Observe(Snap(10, new[] { Tank(1, 3, 3, 0.2) }), Rules, changes);   // first sight, already burning
        Assert.Empty(changes);
        tracker.Observe(Snap(10, new[] { Tank(1, 3, 3, 0.1) }), Rules, changes);   // same tick: ignored
        Assert.Empty(changes);
        tracker.Observe(Snap(11, new[] { Tank(1, 3, 3, 0.9) }), Rules, changes);   // repaired: no burst
        Assert.Empty(changes);
        tracker.Observe(Snap(12, new[] { Tank(1, 3, 3, 0.3), Tank(7, 6, 6, 0.1) }), Rules, changes);   // a second unit revealed while hurt: quiet; the first goes down a tier
        Assert.Equal(new[] { new TierChange(false, 0, DamageTier.None, DamageTier.Smoke) }, changes);
        tracker.Observe(Snap(5, new[] { Tank(1, 3, 3, 0.1) }), Rules, changes);   // time went backwards (load): quiet
        Assert.Empty(changes);
    }

    [Fact]
    public void TrackerIgnoresGhostsAndInfantry()
    {
        var tracker = new DamageTierTracker();
        var changes = new List<TierChange>();
        tracker.Observe(Snap(1, buildings: new[] { Barracks(2, 5, 5, 1.0, ghost: true) }), Rules, changes);
        tracker.Observe(Snap(2, buildings: new[] { Barracks(2, 5, 5, 0.1, ghost: true) }), Rules, changes);
        Assert.Empty(changes);
    }

    // ---- remains: timing, caps -----------------------------------------------------------------------------------------------------

    [Fact]
    public void RemainsLivesAreTheDocumentsAssumedSeconds()
    {
        Assert.Equal(30 * 20, RemainsTiming.Life(RemainKind.BuildingRubble));
        Assert.Equal(15 * 20, RemainsTiming.Life(RemainKind.VehicleWreck));
        Assert.Equal(30 * 20, RemainsTiming.Life(RemainKind.Scorch));
        Assert.True(RemainsTiming.Life(RemainKind.InfantryMark) < RemainsTiming.Life(RemainKind.VehicleWreck));
        Assert.Equal(64, RemainsTiming.Cap(RemainKind.Scorch));
    }

    [Theory]
    [InlineData(RemainKind.BuildingRubble)]
    [InlineData(RemainKind.VehicleWreck)]
    [InlineData(RemainKind.Scorch)]
    [InlineData(RemainKind.InfantryMark)]
    public void RemainsFadeIn_Hold_FadeOut(RemainKind kind)
    {
        int life = RemainsTiming.Life(kind), max = RemainsTiming.MaxAlpha(kind);
        Assert.Equal(0, RemainsTiming.Alpha(kind, -1));
        Assert.InRange(RemainsTiming.Alpha(kind, 0), 1, max - 1);                   // starts faint and settles in
        Assert.Equal(max, RemainsTiming.Alpha(kind, RemainsTiming.FadeInTicks + 1));
        Assert.Equal(max, RemainsTiming.Alpha(kind, life * RemainsTiming.HoldPermille / 1000));   // full strength through the hold
        int previous = max;
        for (int age = life * RemainsTiming.HoldPermille / 1000; age < life; age++)
        {
            int a = RemainsTiming.Alpha(kind, age);
            Assert.True(a <= previous, $"age {age}: {a} > {previous}");
            previous = a;
        }

        Assert.InRange(RemainsTiming.Alpha(kind, life - 1), 1, max / 8 + 1);          // almost gone on the last tick
        Assert.Equal(0, RemainsTiming.Alpha(kind, life));
        Assert.Equal(0, RemainsTiming.Alpha(kind, life + 500));
    }

    [Fact]
    public void RemainsAreCappedPerKindAndTheOldestOfThatKindGoesFirst()
    {
        var book = new RemainsBook();
        book.Add(new Remain(RemainKind.BuildingRubble, 0, 0, 2, 2, 0, 1, 0));
        for (int i = 0; i < RemainsTiming.MaxWrecks + 10; i++) book.Add(new Remain(RemainKind.VehicleWreck, i, 0, 0, 0, 0, 1, i));
        Assert.Equal(RemainsTiming.MaxWrecks, book.Items.Count(r => r.Kind == RemainKind.VehicleWreck));
        Assert.Equal(10, book.Items.Where(r => r.Kind == RemainKind.VehicleWreck).Min(r => r.X));   // wrecks 0..9 were dropped
        Assert.Single(book.Items, r => r.Kind == RemainKind.BuildingRubble);                       // other kinds are untouched
        for (int i = 0; i < RemainsTiming.MaxScorch + 5; i++) book.Add(new Remain(RemainKind.Scorch, i, 0, 30, 643, 0, 1, i));
        Assert.Equal(RemainsTiming.MaxScorch, book.Items.Count(r => r.Kind == RemainKind.Scorch));
    }

    [Fact]
    public void RemainsExpireAndAreDroppedAfterARestart()
    {
        var book = new RemainsBook();
        book.Add(new Remain(RemainKind.VehicleWreck, 1, 1, 0, 0, 0, 1, 100));
        book.Add(new Remain(RemainKind.BuildingRubble, 2, 2, 2, 2, 0, 1, 100));
        book.Prune(100 + RemainsTiming.VehicleWreckTicks - 1);
        Assert.Equal(2, book.Items.Count);
        book.Prune(100 + RemainsTiming.VehicleWreckTicks);
        Assert.Single(book.Items, r => r.Kind == RemainKind.BuildingRubble);
        book.Prune(50);   // a future remain (the game was restarted from an earlier tick)
        Assert.Empty(book.Items);
    }

    // ---- remains: deaths -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABuildingThatVanishesWithItsDeathEventLeavesRubbleOnItsFootprint()
    {
        var watcher = new DeathWatcher();
        var deaths = new List<DeathRecord>();
        BuildingView b = Barracks(2, 5, 6);
        watcher.Observe(Snap(10, buildings: new[] { b }), deaths);
        watcher.Observe(Snap(11, fx: new[] { new FxEvent(FxKind.BuildingDestroyed, Cell(6, 7), 2) }), deaths);
        DeathRecord d = Assert.Single(deaths);
        Assert.Equal(RemainKind.BuildingRubble, d.Kind);
        Assert.Equal(new CellCoord(5, 6), d.Cell);
        Assert.Equal((2, 2), (d.Width, d.Height));
        Assert.Equal(Cell(6, 7), d.Pos);
    }

    [Fact]
    public void ASoldOrHiddenBuildingLeavesNothing()
    {
        var watcher = new DeathWatcher();
        var deaths = new List<DeathRecord>();
        watcher.Observe(Snap(10, buildings: new[] { Barracks(2, 5, 6) }, units: new[] { Tank(1, 3, 3) }), deaths);
        watcher.Observe(Snap(11, fx: new[] { new FxEvent(FxKind.BuildingDestroyed, Cell(14, 14), 2), new FxEvent(FxKind.Detonation, Cell(3, 3), 1) }), deaths);   // far event, wrong kind
        Assert.Empty(deaths);
    }

    [Fact]
    public void AVehicleThatVanishesNextToItsDeathEventLeavesAWreckAndOneEventServesOneUnit()
    {
        var watcher = new DeathWatcher();
        var deaths = new List<DeathRecord>();
        watcher.Observe(Snap(10, units: new[] { Tank(1, 3, 3), Tank(2, 3.2, 3) }), deaths);
        watcher.Observe(Snap(11, fx: new[] { new FxEvent(FxKind.UnitDestroyed, Cell(3.1, 3.1), 1) }), deaths);
        DeathRecord d = Assert.Single(deaths);   // two units vanished, one death: only one wreck
        Assert.Equal(RemainKind.VehicleWreck, d.Kind);
        Assert.InRange(d.Pos.X, Cell(3, 0).X, Cell(3.2, 0).X);
    }

    [Fact]
    public void ASoldierLeavesAMarkAndTheFirstSnapshotIsQuiet()
    {
        var watcher = new DeathWatcher();
        var deaths = new List<DeathRecord>();
        var rifle = new UnitView(new EntityId(9, 1), UnitKind.Infantry, 0, 0, Cell(4, 4), 0, 50);
        watcher.Observe(Snap(10, units: new[] { rifle }, fx: new[] { new FxEvent(FxKind.UnitDestroyed, Cell(8, 8), 1) }), deaths);   // nothing to compare with
        Assert.Empty(deaths);
        watcher.Observe(Snap(11, fx: new[] { new FxEvent(FxKind.UnitDestroyed, Cell(4.5, 4), 1) }), deaths);
        Assert.Equal(RemainKind.InfantryMark, Assert.Single(deaths).Kind);
    }

    [Fact]
    public void AGhostBuildingIsNotAFootprintToBury()
    {
        var watcher = new DeathWatcher();
        var deaths = new List<DeathRecord>();
        watcher.Observe(Snap(10, buildings: new[] { Barracks(2, 5, 6, ghost: true) }), deaths);
        watcher.Observe(Snap(11, fx: new[] { new FxEvent(FxKind.BuildingDestroyed, Cell(6, 7), 2) }), deaths);
        Assert.Empty(deaths);
    }

    [Fact]
    public void ASimWreckOnTheCellSuppressesTheDrawnWreck()
    {
        var remain = new Remain(RemainKind.VehicleWreck, 3 * 64 + 20, 5 * 64 + 40, 0, 0, 3, 1, 0);
        Assert.False(FxLook.HasSimWreck(null, remain));
        Assert.False(FxLook.HasSimWreck(new[] { new WreckDraw(new CellCoord(4, 5), 100, 1) }, remain));
        Assert.True(FxLook.HasSimWreck(new[] { new WreckDraw(new CellCoord(4, 5), 100, 1), new WreckDraw(new CellCoord(3, 5), 100, 2) }, remain));
    }

    // ---- dust ----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void DustRateRisesWithSpeedAndIsZeroWhenSlow()
    {
        Assert.Equal(0, DustRate.PeriodTicks(0));
        Assert.Equal(0, DustRate.PeriodTicks(DustRate.MinSpeed - 1));
        int previous = int.MaxValue;
        for (int speed = DustRate.MinSpeed; speed <= 80; speed++)
        {
            int p = DustRate.PeriodTicks(speed);
            Assert.InRange(p, DustRate.MinPeriod, DustRate.MaxPeriod);
            Assert.True(p <= previous);
            previous = p;
        }

        Assert.Equal(DustRate.MaxPeriod, DustRate.PeriodTicks(DustRate.MinSpeed));
        Assert.Equal(DustRate.MinPeriod, DustRate.PeriodTicks(80));
    }

    [Fact]
    public void DustPuffsAreDueOncePerPeriodAndCatchUp()
    {
        int period = 4, due = 0;
        for (int t = 1; t <= 40; t++) if (DustRate.Due(period, 0, t - 1, t)) due++;
        Assert.Equal(10, due);
        Assert.True(DustRate.Due(period, 0, 5, 9));     // two ticks skipped by a fast game speed: one puff still due
        Assert.False(DustRate.Due(0, 0, 1, 9));         // period 0 = no dust
        Assert.NotEqual(Enumerable.Range(1, 12).Where(t => DustRate.Due(period, 0, t - 1, t)), Enumerable.Range(1, 12).Where(t => DustRate.Due(period, 2, t - 1, t)));   // phase shifts a vehicle's puffs
    }

    [Fact]
    public void OnlyWheelsAndTracksOnOpenGroundRaiseDust()
    {
        Assert.True(DustRate.Dusty(Locomotion.Tracked));
        Assert.True(DustRate.Dusty(Locomotion.Wheeled));
        foreach (Locomotion l in new[] { Locomotion.Hover, Locomotion.Air, Locomotion.Foot, Locomotion.Burrow, Locomotion.Naval }) Assert.False(DustRate.Dusty(l), l.ToString());
        foreach (LandType l in new[] { LandType.Clear, LandType.Ash, LandType.Rough }) Assert.True(DustRate.DustyGround(l));
        foreach (LandType l in new[] { LandType.Road, LandType.Water, LandType.Rock, LandType.Cliff, LandType.Shallow }) Assert.False(DustRate.DustyGround(l));
    }

    private static int PuffsFromAColumn(string type, int speedLeptonsPerTick, LandType land = LandType.Clear, int ticks = 40)
    {
        var emitter = new DustEmitter();
        var field = new PuffField();
        for (int t = 0; t <= ticks; t++)
        {
            double x = 2 + t * speedLeptonsPerTick / 256.0;
            emitter.Observe(Snap(t, units: new[] { Tank(1, x, 5, 1.0, type) }, land: land), Rules, field, 0xD0C3A4);
        }

        return field.Items.Count;
    }

    [Fact]
    public void AMovingTankRaisesDustAndAStandingOneDoesNot()
    {
        Assert.InRange(PuffsFromAColumn("auth_tank", 14), 5, 15);
        Assert.Equal(0, PuffsFromAColumn("auth_tank", 0));
        Assert.True(PuffsFromAColumn("auth_jeep", 30) > PuffsFromAColumn("auth_tank", 8));   // faster, more puffs
    }

    [Fact]
    public void HoverVehiclesAndWaterRaiseNoDust()
    {
        Assert.Equal(0, PuffsFromAColumn("exp_glide_tank", 30));
        Assert.Equal(0, PuffsFromAColumn("auth_tank", 14, LandType.Water));
        Assert.Equal(0, PuffsFromAColumn("auth_tank", 14, LandType.Road));
    }

    [Fact]
    public void PuffFieldCapsPrunesAndPicksFrames()
    {
        var field = new PuffField();
        for (int i = 0; i < PuffField.MaxPuffs + 20; i++) field.Add(new Puff(i, 0, i, 20, 0xFFFFFF, 100, 1, 10));
        Assert.Equal(PuffField.MaxPuffs, field.Items.Count);
        Assert.Equal(20, (int)field.Items[0].X);
        field.Prune(field.Items[^1].Born + 5);
        Assert.All(field.Items, p => Assert.True(p.Born + p.Life > field.Items[^1].Born + 5));
        Assert.Equal(0, PuffField.Frame(0, 20));
        Assert.Equal(PuffArt.Frames - 1, PuffField.Frame(19, 20));
        Assert.Equal(100, PuffField.Alpha(0, 20, 100));
        Assert.Equal(0, PuffField.Alpha(20, 20, 100));
    }

    // ---- mood ----------------------------------------------------------------------------------------------------------------------

    private static string TheaterFile(string name)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidate = Path.Combine(dir, "data", "theaters", name + ".toml");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        throw new FileNotFoundException(name);
    }

    [Theory]
    [InlineData("ash_plains")]
    [InlineData("mountains")]
    [InlineData("glass_coast")]
    [InlineData("rustbelt")]
    public void EveryShippedTheaterHasASubtleMood(string file)
    {
        TerrainPalette p = TerrainPalette.Load(TheaterFile(file));
        Assert.InRange(p.Mood.Strength, 10, 60);   // subtle
        Assert.NotNull(p.Mood.Dust);
    }

    [Fact]
    public void TheMoodsDiffer()
    {
        var tints = new[] { "ash_plains", "mountains", "glass_coast", "rustbelt" }.Select(f => TerrainPalette.Load(TheaterFile(f)).Mood.Tint).ToArray();
        Assert.Equal(4, tints.Distinct().Count());
    }

    [Fact]
    public void NoMoodTableMeansNoGrade_AndBadValuesAreTamed()
    {
        Assert.Equal(MoodGrade.None, MoodGrade.Parse(Toml("a = 1")));
        Assert.False(MoodGrade.None.Active);
        MoodGrade huge = MoodGrade.Parse(Toml("[mood]\ntint = \"#102030\"\nstrength = 5000\ndust = \"nope\""));
        Assert.Equal(MoodGrade.MaxStrength, huge.Strength);
        Assert.Equal(0x102030u, huge.Tint);
        Assert.Null(huge.Dust);
        Assert.Equal(MoodGrade.DefaultDust, huge.DustColour);
        Assert.Equal(0, MoodGrade.Parse(Toml("[mood]\nstrength = -4")).Strength);
        Assert.False(MoodGrade.Parse(Toml("[mood]\nstrength = 0")).Active);
    }

    [Fact]
    public void TheClassicAndPlainLooksStayUngraded()
    {
        TerrainPalette classic = TerrainPalette.Load(TheaterFile("rustbelt").Replace("theaters" + Path.DirectorySeparatorChar + "rustbelt", "theaters" + Path.DirectorySeparatorChar + "variants" + Path.DirectorySeparatorChar + "classic" + Path.DirectorySeparatorChar + "rustbelt"));
        Assert.False(classic.Mood.Active);
    }

    private static TomlTable Toml(string text) => TomlSerializer.Deserialize<TomlTable>(text)!;

    // ---- art -----------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void RemainsArtIsDeterministicAndFactionTinted()
    {
        Assert.Equal(RemainsArt.Rubble(2, 2, 0, 5).Rgba, RemainsArt.Rubble(2, 2, 0, 5).Rgba);
        Assert.NotEqual(RemainsArt.Rubble(2, 2, 0, 5).Rgba, RemainsArt.Rubble(2, 2, 3, 5).Rgba);   // palette only
        Assert.NotEqual(RemainsArt.Rubble(2, 2, 0, 5).Rgba, RemainsArt.Rubble(2, 2, 0, 6).Rgba);   // variants differ
        PropCanvas rubble = RemainsArt.Rubble(3, 2, 1, 1);
        Assert.Equal(RemainsArt.RubbleSize(3, 2), (rubble.Width, rubble.Height));
        Assert.Equal((3 * 64 + 16, 2 * 64 + 16), (rubble.Width, rubble.Height));
        Assert.Equal((RemainsArt.WreckW, RemainsArt.WreckH), (RemainsArt.Wreck(0, 1).Width, RemainsArt.Wreck(0, 1).Height));
        Assert.NotEqual(RemainsArt.Wreck(0, 1).Rgba, RemainsArt.Wreck(4, 1).Rgba);
        Assert.NotEqual(RemainsArt.Wreck(0, 1).Rgba, RemainsArt.Wreck(0, 2).Rgba);   // a seed bit mirrors the wreck
    }

    [Fact]
    public void ArtDrawsInsideItsFrameAndStaysDark()
    {
        foreach (PropCanvas c in new[] { RemainsArt.Rubble(2, 2, 0, 1), RemainsArt.Wreck(0, 1), RemainsArt.Mark(1), RemainsArt.Scorch(36, 643, 1) })
        {
            int painted = 0;
            for (int i = 3; i < c.Rgba.Length; i += 4) if (c.Rgba[i] != 0) painted++;
            Assert.InRange(painted, c.Width * c.Height / 40, c.Width * c.Height * 9 / 10);
        }

        // a smudge and a scorch are dark and carry no red: nothing gory
        foreach (PropCanvas c in new[] { RemainsArt.Mark(1), RemainsArt.Scorch(36, 643, 1) })
            for (int i = 0; i < c.Rgba.Length; i += 4) if (c.Rgba[i + 3] != 0) Assert.True(c.Rgba[i] <= c.Rgba[i + 3] / 3 + 1 && c.Rgba[i] >= c.Rgba[i + 1] && c.Rgba[i + 1] >= c.Rgba[i + 2] - 1);
    }

    [Fact]
    public void ScorchUsesHardSteppedAlphaOnly()
    {
        var alphas = new HashSet<byte>();
        PropCanvas c = RemainsArt.Scorch(40, 643, 3);
        for (int i = 3; i < c.Rgba.Length; i += 4) alphas.Add(c.Rgba[i]);
        Assert.True(alphas.Count <= 5, string.Join(",", alphas));   // 0 plus the four steps: no soft gradient
    }

    [Fact]
    public void PuffArtIsGreyAndDitheredAtTheEnd()
    {
        byte[] atlas = PuffArt.Render();
        int Opaque(int frame)
        {
            int n = 0;
            for (int y = 0; y < PuffArt.FramePx; y++)
                for (int x = 0; x < PuffArt.FramePx; x++)
                {
                    int o = (y * PuffArt.Frames * PuffArt.FramePx + frame * PuffArt.FramePx + x) * 4;
                    if (atlas[o + 3] == 0) continue;
                    Assert.Equal(255, atlas[o + 3]);                              // hard alpha
                    Assert.True(atlas[o] == atlas[o + 1] && atlas[o + 1] == atlas[o + 2]);   // grey: the puff's colour tints it
                    n++;
                }
            return n;
        }

        Assert.True(Opaque(0) > 0);
        Assert.True(Opaque(3) > 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KenneyPuffAtlasesKeepThePuffContract(bool smoke)
    {
        byte[] atlas = PuffArt.Render(smoke);
        Assert.Equal(PuffArt.Frames * PuffArt.FramePx * PuffArt.FramePx * 4, atlas.Length);
        int[] solid = new int[PuffArt.Frames];
        for (int y = 0; y < PuffArt.FramePx; y++)
            for (int x = 0; x < PuffArt.Frames * PuffArt.FramePx; x++)
            {
                int o = (y * PuffArt.Frames * PuffArt.FramePx + x) * 4;
                if (atlas[o + 3] == 0) continue;
                Assert.Equal(255, atlas[o + 3]);
                Assert.True(atlas[o] == atlas[o + 1] && atlas[o + 1] == atlas[o + 2]);
                solid[x / PuffArt.FramePx]++;
            }

        Assert.All(solid, n => Assert.True(n > 20));
        Assert.True(solid[1] > solid[0]);   // the puff grows
    }

    [Theory]
    [InlineData("puffs_dust.png")]
    [InlineData("puffs_smoke.png")]
    public void KenneyPuffSheetsOnDiskAreGreyHardAlphaAndFourFrames(string file)
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sprites", "fx", file));
        if (!File.Exists(path)) return;
        (byte[] rgba, int w, int h) = PngRgba.Load(path);
        Assert.Equal((PuffArt.Frames * PuffArt.FramePx, PuffArt.FramePx), (w, h));
        for (int i = 0; i < rgba.Length; i += 4)
        {
            Assert.True(rgba[i + 3] is 0 or 255);
            if (rgba[i + 3] == 255) Assert.True(rgba[i] == rgba[i + 1] && rgba[i + 1] == rgba[i + 2]);
        }
    }

    // ---- setting and size ----------------------------------------------------------------------------------------------------------

    [Fact]
    public void LargerInfantryDefaultsOnAndRoundTrips()
    {
        Assert.True(Settings.Default.LargerInfantry);
        Assert.True(Settings.Parse("version = 2").LargerInfantry);
        Settings off = Settings.Default with { LargerInfantry = false };
        Assert.False(Settings.Parse(off.ToToml()).LargerInfantry);
        Assert.True(Settings.Parse(Settings.Default.ToToml()).LargerInfantry);
        Assert.True(Settings.Parse("larger_infantry = \"yes\"").LargerInfantry);   // wrong type falls back to the default
    }

    [Fact]
    public void LargerInfantryScalesInfantryOnly()
    {
        float before = UnitRenderer.InfantryExtraScale;
        try
        {
            var inf = new UnitView(new EntityId(1, 1), UnitKind.Infantry, 0, 0, Cell(1, 1), 0, 10);
            UnitRenderer.InfantryExtraScale = 1f;
            Assert.Equal(1f, UnitRenderer.DrawScale(Rules, inf));
            UnitRenderer.InfantryExtraScale = UnitRenderer.LargerInfantryScale;
            Assert.Equal(1.25f, UnitRenderer.DrawScale(Rules, inf));
            Assert.Equal(1f, UnitRenderer.DrawScale(Rules, Tank(2, 1, 1)));   // a tank is unchanged
        }
        finally
        {
            UnitRenderer.InfantryExtraScale = before;
        }
    }
}
