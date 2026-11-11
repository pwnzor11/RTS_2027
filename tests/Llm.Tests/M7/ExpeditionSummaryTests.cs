// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Llm;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.Shipments;
using Rts.Sim.View;

namespace Llm.Tests.M7;

/// <summary>M7-10: the Expedition fields (shipment timer, payload, beachhead size, convoy danger) and the naval threat line of
/// <see cref="BattlefieldSummary"/>. Small fixture: a 40x20 map with two water bodies (x less than 10 and x of 30 or more), a dock on
/// land at (12,10) that is also a beachhead source, one own supply tender that stays at its launch cell (no water beside the dock), and
/// enemy ships placed by the test. Player 0 is the viewer; player 1 is the enemy.</summary>
public sealed class ExpeditionSummaryTests
{
    private const int GunboatT = 1, SubT = 2, DockT = 0;
    private const int W = 40, H = 20;
    private static readonly CellCoord DockCell = new(12, 10);
    private static readonly string[] ExpeditionFields = { "EXPEDITION:", "shipment_eta:", "shipment_payload:", "beachhead_cells:", "convoy_in_danger:" };

    private static SimRules Rules(bool sonar)
    {
        var tender = new VehicleType("exp_tender", Locomotion.Naval, 1, 8, 100, ArmorClass.Heavy, 5, -1, false, 0, false, 0, Array.Empty<string>())
            with { SonarRadius = sonar ? 6 : 0 };
        var gunboat = new VehicleType("gunboat", Locomotion.Naval, 60, 8, 200, ArmorClass.Heavy, 5, -1, false, 0, false, 300, Array.Empty<string>());
        var sub = new VehicleType("sub", Locomotion.Naval, 40, 8, 150, ArmorClass.Heavy, 4, -1, false, 0, false, 500, Array.Empty<string>())
            with { Submerges = true };
        var dock = new BuildingType("dock", 2, 2, 300, ArmorClass.Structure, 5, -1, 0, 0, Array.Empty<string>(), Array.Empty<string>())
            with { ShipmentDock = true, BeachheadSource = true, BeachheadRadius = 3 };
        return new SimRules(new[] { tender, gunboat, sub }, Array.Empty<InfantryType>(), new[] { dock }, Array.Empty<WeaponType>(),
            Array.Empty<WarheadType>(), Terrain(), Array.Empty<EffectDef>())
        {
            Shipments = new ShipmentRules { Enabled = true, TenderType = "exp_tender", IntervalTicks = 100, Value = 1000, DecayPermille = 300,
                FloorPermille = 350, CharterTicks = 0, TendersMax = 3 },
        };
    }

    private static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        for (int land = 0; land < RuleCounts.LandTypes; land++) speed[land * RuleCounts.Locomotions + (int)Locomotion.Naval] = 0;
        speed[(int)LandType.Water * RuleCounts.Locomotions + (int)Locomotion.Naval] = 100;
        speed[(int)LandType.Water * RuleCounts.Locomotions + (int)Locomotion.Foot] = 0;
        speed[(int)LandType.Water * RuleCounts.Locomotions + (int)Locomotion.Tracked] = 0;
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true;
        return new TerrainTable(speed, buildable);
    }

    private static MapData Map()
    {
        var land = new LandType[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                land[y * W + x] = x < 10 || x >= 30 ? LandType.Water : LandType.Clear;
        return new MapData("sea", "rust_belt", W, H, new ushort[W * H], land, new byte[W * H], new OverlayKind[W * H], new byte[W * H],
            new[] { new CellCoord(1, 1), new CellCoord(W - 2, H - 2) });
    }

    /// <summary>A started game: player 0 is an Expedition player when <paramref name="expedition"/>, else Authority.</summary>
    private static Simulation NewSim(bool expedition = true, bool sonar = false, bool dock = true)
    {
        var setup = new SimSetup(1, Map(), 2, Rules(sonar), 0xABCDUL) { AllowDebug = true }
            with { Factions = expedition ? new[] { Faction.Expedition, Faction.Authority } : null };
        var sim = new Simulation(setup);
        if (dock) Assert.False(sim.PlaceBuilding(0, DockT, DockCell).IsNone);
        return sim;
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++) sim.Step(Array.Empty<Command>());
    }

    private static string Summary(Simulation sim, SummaryHistory h)
    {
        h.Observe(sim.ViewFor(0));
        return BattlefieldSummary.Build(sim.ViewFor(0), h);
    }

    private static ushort _seq = 50000;

    /// <summary>Spawns an enemy (player 1) vehicle through a debug command, so it works after the first step.</summary>
    private static EntityId Enemy(Simulation sim, int type, CellCoord cell)
    {
        int slot = 1;
        while (sim.Units.Slots.IsIndexAlive(slot)) slot++;
        sim.Step(new[] { new Command(sim.Tick, 1, _seq++, CommandKind.Debug, default, default, cell, (int)DebugOp.SpawnVehicle, type) });
        return sim.Units.Slots.IdOf(slot);
    }

    [Fact]
    public void Expedition_summary_has_the_expedition_fields_with_real_values()
    {
        var sim = NewSim();
        Run(sim, 5);
        var h = new SummaryHistory();
        string text = Summary(sim, h);
        foreach (string f in ExpeditionFields) Assert.Contains(f, text);
        Assert.Matches(@"shipment_eta: \d+s", text);
        Assert.Contains("shipment_payload: 1000cr", text);
        Assert.Contains("convoy_in_danger: false", text);

        var view = sim.ViewFor(0);
        int expected = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                if (view.InBeachhead(new CellCoord(x, y))) expected++;
        Assert.True(expected > 0);
        Assert.Contains("beachhead_cells: " + expected + "\n", text);
    }

    [Fact]
    public void Shipment_eta_is_none_without_a_dock()
    {
        var sim = NewSim(dock: false);
        Run(sim, 5);
        string text = Summary(sim, new SummaryHistory());
        Assert.Contains("shipment_eta: none", text);
    }

    [Fact]
    public void Non_expedition_summary_has_none_of_the_expedition_fields()
    {
        var sim = NewSim(expedition: false);
        Run(sim, 5);
        string text = Summary(sim, new SummaryHistory());
        foreach (string f in ExpeditionFields) Assert.DoesNotContain(f, text);
        Assert.DoesNotContain("shipment_", text);
        Assert.DoesNotContain("naval_threat", text);   // no naval unit in view: the line is only written when non-zero
    }

    [Fact]
    public void Convoy_in_danger_flips_when_a_visible_enemy_is_near_a_tender()
    {
        var sim = NewSim();
        Run(sim, 110);   // first launch: NextShipmentTick is set on the first tick, then one interval later
        var tenders = new List<TenderView>();
        sim.ViewFor(0).CopyOwnTenders(tenders);
        TenderView tender = Assert.Single(tenders);
        var h = new SummaryHistory();
        Assert.Contains("convoy_in_danger: false", Summary(sim, h));

        // a gunboat three cells from the tender, on the west water (x < 10): in the tender's own sight, so the viewer sees it.
        // S19 M13-03: the tender spawns at the edge cell nearest the dock, so place relative to wherever it is.
        var at = new CellCoord(tender.Cell.X >= 3 ? tender.Cell.X - 3 : tender.Cell.X + 3, tender.Cell.Y < H / 2 ? tender.Cell.Y + 1 : tender.Cell.Y - 1);
        Assert.False(Enemy(sim, GunboatT, at).IsNone);
        Run(sim, 1);
        Assert.True(sim.ViewFor(0).IsVisible(at), $"the gunboat must be visible for this test (tender {tender.Cell.X},{tender.Cell.Y}, at {at.X},{at.Y})");
        string text = Summary(sim, h);
        Assert.Contains("convoy_in_danger: true", text);
        Assert.Contains("naval_threat: 1 (1 surface, 0 dived), strength ~300cr", text);
    }

    [Fact]
    public void Fog_an_unseen_ship_is_not_counted_and_does_not_endanger_the_convoy()
    {
        var sim = NewSim();
        Run(sim, 110);
        var far = new CellCoord(33, 10);   // the east lake: outside every own sight
        Assert.False(Enemy(sim, GunboatT, far).IsNone);
        Run(sim, 1);
        var view = sim.ViewFor(0);
        Assert.False(view.IsVisible(far));
        string text = Summary(sim, new SummaryHistory());
        Assert.DoesNotContain("naval_threat", text);
        Assert.DoesNotContain("gunboat", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("convoy_in_danger: false", text);
    }

    [Fact]
    public void Fog_a_dived_submarine_the_viewer_cannot_detect_is_not_counted()
    {
        var sim = NewSim(sonar: false);
        Run(sim, 110);
        EntityId sub = Enemy(sim, SubT, new CellCoord(3, 3));
        Assert.False(sub.IsNone);
        sim.Step(new[] { new Command(sim.Tick, 1, _seq++, CommandKind.Deploy, sub, default, default, 0, 0) });   // dive
        Run(sim, 1);
        Assert.DoesNotContain("naval_threat", Summary(sim, new SummaryHistory()));
    }

    [Fact]
    public void Detected_dived_submarine_counts_as_naval_threat()
    {
        var sim = NewSim(sonar: true);   // the own tender's sonar (radius 6) reveals the submerged enemy
        Run(sim, 110);
        var tenders = new List<TenderView>();
        sim.ViewFor(0).CopyOwnTenders(tenders);
        CellCoord tc = Assert.Single(tenders).Cell;   // S19 M13-03: place the sub within the tender's sonar wherever it spawned
        EntityId sub = Enemy(sim, SubT, new CellCoord(tc.X >= 3 ? tc.X - 3 : tc.X + 3, tc.Y < H / 2 ? tc.Y + 2 : tc.Y - 2));
        sim.Step(new[] { new Command(sim.Tick, 1, _seq++, CommandKind.Deploy, sub, default, default, 0, 0) });
        Run(sim, 1);
        string text = Summary(sim, new SummaryHistory());
        Assert.Contains("naval_threat: 1 (0 surface, 1 dived), strength ~500cr", text);
    }

    [Fact]
    public void Naval_threat_is_reported_for_a_non_expedition_faction_too()
    {
        var sim = NewSim(expedition: false);   // Authority viewer; the dock gives vision over the west water
        Assert.False(Enemy(sim, GunboatT, new CellCoord(8, 10)).IsNone);
        Run(sim, 2);
        string text = Summary(sim, new SummaryHistory());
        Assert.Contains("naval_threat: 1 (1 surface, 0 dived), strength ~300cr", text);
        Assert.DoesNotContain("EXPEDITION:", text);
    }
}
