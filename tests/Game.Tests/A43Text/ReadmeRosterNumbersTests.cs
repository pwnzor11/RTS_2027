// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 TEXT-05: the README is hand-typed, so its costs, unit count and Archive count must match data/rules and data/archive.</summary>
public sealed class ReadmeRosterNumbersTests
{
    private static string Readme => File.ReadAllText(Path.Combine(RepoPath.Root, "README.md"));

    [Fact]
    public void Unit_and_archive_counts_match_the_data()
    {
        string readme = Readme;
        Assert.Contains("77 units", readme);
        Assert.DoesNotContain("76 units", readme);
        Assert.Contains("244-entry Archive", readme);
        Assert.Contains("244 entries", readme);
        Assert.DoesNotContain("243", readme);
        Assert.Contains("| Units / structures / upgrades | 15 / 18 / 7 | 18 / 19 / 7 | 17 / 15 / 6 | 27 / 20 / 8 |", readme);
        Assert.Contains("| Grapeshot | 650 | 2 | Hover anti-infantry |", readme);
    }

    [Fact]
    public void Roster_costs_match_the_data()
    {
        string readme = Readme;
        (string Row, string Stale)[] pairs =
        {
            ("| Marksman | 200 |", "| Marksman | 100 |"), ("| Bulwark Heavy Tank | 900 |", "| Bulwark Heavy Tank | 800 |"),
            ("| Ashfall Refinery | 1700 |", "| Ashfall Refinery | 2000 |"), ("| Rocket Truck | 970 |", "| Rocket Truck | 800 |"),
            ("| Reactor Vent | 260 |", "| Reactor Vent | 300 |"), ("| Smelter Vent | 1700 |", "| Smelter Vent | 2000 |"),
            ("| Muster Hatch | 260 |", "| Muster Hatch | 300 |"), ("| Assembly Bore | 1700 |", "| Assembly Bore | 2000 |"),
            ("| Cistern | 130 |", "| Cistern | 150 |"), ("| Bunker Wall | 40 |", "| Bunker Wall | 50 |"),
            ("| Pillbox | 340 |", "| Pillbox | 400 |"), ("| Scavenger | 54 |", "| Scavenger | 60 |"),
            ("| Glide Tank | 960 |", "| Glide Tank | 900 |"), ("| Packed Rig | 1500 |", "| Packed Rig | 4000 |"),
            ("| Supply Tender | 1500 |", "| Supply Tender | 6000 |"), ("| Command Ship | 1500 |", "| Command Ship | 6000 |"),
        };
        foreach (var (row, stale) in pairs)
        {
            Assert.Contains(row, readme);
            Assert.DoesNotContain(stale, readme);
        }
    }
}
