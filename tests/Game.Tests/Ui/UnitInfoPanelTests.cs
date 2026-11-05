// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;

namespace Rts.Game.Tests.Ui;

/// <summary>VET-5: the own-unit info panel text (name, health, rank and kill progress).</summary>
public sealed class UnitInfoPanelTests
{
    [Fact]
    public void VeterancyOffShowsOnlyNameAndHealth()
    {
        Assert.Equal(new[] { "Medium Tank", "Health 80/100" },
            UnitInfoPanel.Lines("Medium Tank", 80, 100, veterancyOn: false, rank: 2, kill: 900, nextThreshold: 3200));
    }

    [Fact]
    public void RankedUnitShowsRankAndProgressToTheNextRank()
    {
        Assert.Equal(new[] { "Medium Tank", "Health 100/100", "Veteran", "Kills 120 / 1600 to Elite" },
            UnitInfoPanel.Lines("Medium Tank", 100, 100, veterancyOn: true, rank: 1, kill: 120, nextThreshold: 1600));
    }

    [Fact]
    public void RecruitShowsProgressToVeteran()
    {
        Assert.Equal(new[] { "Jeep", "Health 40/60", "Kills 120 / 600 to Veteran" },
            UnitInfoPanel.Lines("Jeep", 40, 60, veterancyOn: true, rank: 0, kill: 120, nextThreshold: 600));
    }

    [Fact]
    public void TopRankShowsOnlyTheRankName()
    {
        Assert.Equal(new[] { "Rifleman", "Health 20/20", "Hero", "Max rank" },
            UnitInfoPanel.Lines("Rifleman", 20, 20, veterancyOn: true, rank: 3, kill: 4000, nextThreshold: 0));
    }

    [Fact]
    public void HealthNeverShowsNegativeNumbers()
    {
        Assert.Equal("Health 0/50", UnitInfoPanel.Lines("Jeep", -5, 50, veterancyOn: false, rank: 0, kill: 0, nextThreshold: 0)[1]);
    }
}
