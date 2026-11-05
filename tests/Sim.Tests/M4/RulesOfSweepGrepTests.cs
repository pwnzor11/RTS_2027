// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Xunit;

namespace Rts.Sim.Tests.M4;

/// <summary>
/// M4-14 guard: every <c>Setup.Rules.</c> read in src/Sim must be on the allow-list below. An upgradable attribute read from the base
/// rules silently ignores upgrades, so a new read has to be a deliberate choice: use <c>RulesOf(owner)</c>, or add the (file, read)
/// pair here with the reason it cannot be upgraded.
/// </summary>
public sealed partial class RulesOfSweepGrepTests
{
    [GeneratedRegex(@"Setup\.Rules\.\w+(?:\.\w+|\[)?")]
    private static partial Regex ReadPattern();

    // (file under src/Sim, normalised read). Non-upgradable = counts, type ids, terrain, global rule blocks (no whitelisted attr).
    private static readonly HashSet<(string, string)> Allowed = new()
    {
        // --- owned by M4-14: count-only / non-upgradable ---
        ("Simulation.cs", "Setup.Rules.Vehicles"), ("Simulation.cs", "Setup.Rules.Vehicles.Count"),   // spawn bounds; health via RulesOf
        ("Simulation.cs", "Setup.Rules.Infantry"), ("Simulation.cs", "Setup.Rules.Infantry.Count"),
        ("Simulation.cs", "Setup.Rules.Upgrades.Count"), ("Simulation.cs", "Setup.Rules.Buildings"),
        ("Simulation.cs", "Setup.Rules.Buildings["),   // footprints only (CanPlace, PlaceBuilding, RemoveBuilding)
        ("Simulation.cs", "Setup.Rules.Terrain"),
        ("Movement/Occupancy.cs", "Setup.Rules.Vehicles["),   // locomotion
        ("Movement/Occupancy.cs", "Setup.Rules.Terrain"),
        ("Simulation.Movement.cs", "Setup.Rules.Vehicles["),   // locomotion
        ("Simulation.Naval.cs", "Setup.Rules.Vehicles"), ("Simulation.Naval.cs", "Setup.Rules.Vehicles["), ("Simulation.Naval.cs", "Setup.Rules.Terrain"),   // M7-03: locomotion and terrain, not upgradable
        ("Simulation.Economy.cs", "Setup.Rules.Economy.RegrowthIntervalTicks"),   // global, not an effect attr
        ("Simulation.Tunnels.cs", "Setup.Rules.Tunnels"), ("Simulation.Tunnels.cs", "Setup.Rules.Tunnels.NetworkCapPoints"),
        ("Simulation.Tunnels.cs", "Setup.Rules.Tunnels.PointsOf"), ("Simulation.Tunnels.cs", "Setup.Rules.Tunnels.TelegraphTicks"),
        ("Simulation.Tunnels.cs", "Setup.Rules.Tunnels.TransitTicks"),   // global tunnel block; per-entrance ticks_per_point already via RulesOf

        // --- not owned by M4-14: follow-up ---
        ("Missions/MoveMission.cs", "Setup.Rules.Terrain"), ("Missions/MoveMission.cs", "Setup.Rules.Foam.SpeedPermille"),
        ("Simulation.BurrowMove.cs", "Setup.Rules.Terrain"), ("Simulation.BurrowMove.cs", "Setup.Rules.Terrain.Buildable"),
        ("Simulation.BurrowMove.cs", "Setup.Rules.Terrain.Soft"),
        ("Simulation.BurrowMove.cs", "Setup.Rules.Tunnels.DiveTicks"), ("Simulation.BurrowMove.cs", "Setup.Rules.Tunnels.EmergingHealthPermille"),
        ("Simulation.BurrowMove.cs", "Setup.Rules.Tunnels.EntranceTelegraphTicks"), ("Simulation.BurrowMove.cs", "Setup.Rules.Tunnels.SurfacingRing"),
        ("Simulation.BurrowMove.cs", "Setup.Rules.Tunnels.TelegraphTicks"),
        ("Simulation.Garrison.cs", "Setup.Rules.Garrison.EjectDamagePermille"),   // owner: Simulation.Garrison.cs, follow-up
        ("Simulation.Ground.cs", "Setup.Rules.Radiation"), ("Simulation.Ground.cs", "Setup.Rules.Radiation.DamageThreshold"),   // owner: Simulation.Ground.cs, follow-up
        ("Simulation.Ground.cs", "Setup.Rules.Stealth.DelayTicks"),
        ("Simulation.Hardening.cs", "Setup.Rules.Buildings"), ("Simulation.Hardening.cs", "Setup.Rules.Terrain"),   // owner: Simulation.Hardening.cs, follow-up
        ("Simulation.Hardening.cs", "Setup.Rules.Upgrades"),
        ("Simulation.Hud.cs", "Setup.Rules.Buildings["), ("Simulation.Hud.cs", "Setup.Rules.Upgrades"),   // owner: Simulation.Hud.cs, follow-up
        ("Simulation.Players.cs", "Setup.Rules.StartKits"),
        ("Simulation.Production.cs", "Setup.Rules.Buildings["), ("Simulation.Production.cs", "Setup.Rules.Economy"),   // owner: Simulation.Production.cs, follow-up
        ("Simulation.Production.cs", "Setup.Rules.Economy.BuildRadius"), ("Simulation.Production.cs", "Setup.Rules.Economy.MaxQueue"),
        ("Simulation.Production.cs", "Setup.Rules.Economy.SellRefundPermille"), ("Simulation.Production.cs", "Setup.Rules.Infantry"),
        ("Simulation.Production.cs", "Setup.Rules.Terrain"), ("Simulation.Production.cs", "Setup.Rules.Vehicles.Count"),
        ("Simulation.Superweapon.cs", "Setup.Rules.Economy.LowPowerSlowdownPermille"),
        ("Simulation.Upgrades.cs", "Setup.Rules.Economy.MaxQueue"), ("Simulation.Upgrades.cs", "Setup.Rules.UpgradeIndex"),   // owner: Simulation.Upgrades.cs, follow-up
        ("Simulation.Upgrades.cs", "Setup.Rules.Upgrades"),
        ("Simulation.Veterancy.cs", "Setup.Rules.Veterancy"),   // VET-1: the [veterancy] block is global; one accessor (VetRules) reads it
        ("Simulation.Supply.cs", "Setup.Rules.Supply"),   // SUP-1: the [supply] block is global; one accessor (SupplyRulesOf) reads it
        ("Simulation.View.cs", "Setup.Rules.Buildings["),   // owner: Simulation.View.cs, follow-up
        ("View/PlayerView.cs", "Setup.Rules.Terrain"),
        // M7-06: the [shipments] block is global (not upgradable); the tender type index and naval terrain are table lookups
        ("Simulation.Shipments.cs", "Setup.Rules.Shipments"), ("Simulation.Shipments.cs", "Setup.Rules.Vehicles"),
        ("Simulation.Shipments.cs", "Setup.Rules.Terrain"), ("View/PlayerView.cs", "Setup.Rules.Shipments.Enabled"),
        ("Simulation.Landing.cs", "Setup.Rules.Terrain"),   // M7-04: terrain table lookups, not upgradable
    };

    private static string SimDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RTS_2027.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("RTS_2027.slnx not found"), "src", "Sim");
    }

    [Fact]
    public void NoSetupRulesReadOutsideTheAllowList()
    {
        string root = SimDir();
        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            string rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.StartsWith("obj/", StringComparison.Ordinal) || rel.StartsWith("bin/", StringComparison.Ordinal)) continue;
            string[] lines = File.ReadAllLines(file);
            for (int n = 0; n < lines.Length; n++)
            {
                string code = lines[n];
                int c = code.IndexOf("//", StringComparison.Ordinal);
                if (c >= 0) code = code[..c];
                foreach (Match m in ReadPattern().Matches(code))
                    if (!Allowed.Contains((rel, m.Value))) offenders.Add($"{rel}:{n + 1}: {m.Value}");
            }
        }
        Assert.True(offenders.Count == 0,
            "Setup.Rules read outside the allow-list (use RulesOf(owner) for upgradable attrs, or allow-list it):\n" + string.Join("\n", offenders));
    }
}
