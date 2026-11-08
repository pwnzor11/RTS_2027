// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Game.Tests.Controls;

/// <summary>M11-10a: a scraper in a default skirmish earns money with no orders at all (the owner reported it did not).</summary>
public sealed class StarterHarvestTests
{
    internal static string RulesDir => RulesLocator.FindRulesDir(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("data/rules not found");

    private static int Index<T>(IReadOnlyList<T> list, Func<T, string> id, string want) => SimRules.IndexOf(list, want, id);

    /// <summary>Player 0's refinery on the first free spot near the conyard, plus its scraper (what a finished refinery gives).</summary>
    internal static (SkirmishGame Game, EntityId Scraper) Setup()
    {
        SkirmishGame game = SkirmishGame.Create(RulesDir, 0, 2);
        Simulation sim = game.Sim;
        int refinery = Index(sim.Setup.Rules.Buildings, b => b.Id, "auth_refinery");
        int scraper = Index(sim.Setup.Rules.Vehicles, v => v.Id, "auth_scraper");
        CellCoord start = sim.Setup.Map.StartPositions[0];
        EntityId placed = EntityId.None;
        for (int r = 4; r < 14 && placed.IsNone; r++)
            for (int dy = -r; dy <= r && placed.IsNone; dy++)
                for (int dx = -r; dx <= r && placed.IsNone; dx++)
                {
                    var tl = new CellCoord(start.X + dx, start.Y + dy);
                    if (sim.CanPlace(0, refinery, tl) == PlacementResult.Ok) placed = sim.PlaceBuilding(0, refinery, tl);
                }
        Assert.False(placed.IsNone);
        CellCoord at = sim.Buildings.Cell[placed.Index];
        EntityId unit = EntityId.None;
        for (int dx = -3; dx <= 6 && unit.IsNone; dx++)
            for (int dy = -3; dy <= 6 && unit.IsNone; dy++)
            {
                var c = new CellCoord(at.X + dx, at.Y + dy);
                if (sim.Setup.Map.Contains(c) && sim.Map.BuildingAt(c).IsNone && sim.Map.VehicleAt(c).IsNone
                    && sim.Map.IsPassableTerrain(c, Locomotion.Tracked, sim.Setup.Rules.Terrain))
                    unit = sim.SpawnVehicle(0, scraper, c);
            }
        Assert.False(unit.IsNone);
        return (game, unit);
    }

    [Fact]
    public void An_unordered_scraper_harvests_and_the_player_earns_money()
    {
        (SkirmishGame game, EntityId unit) = Setup();
        var host = new SimHost(game.Sim);
        int before = game.Sim.Funds(0);
        bool harvested = false;
        for (int t = 0; t < 4000 && !harvested; t += 20)
        {
            host.StepTicks(20);
            harvested = game.Sim.Funds(0) > before;
        }
        Assert.True(harvested, $"funds stayed at {before}; mission {game.Sim.Units.Mission[unit.Index]}");
    }
}
