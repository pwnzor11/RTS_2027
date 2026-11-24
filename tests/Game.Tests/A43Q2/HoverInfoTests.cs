// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui;
using Xunit;

namespace Rts.Game.Tests.A43Q2;

public sealed class HoverInfoTests
{
    [Fact]
    public void A_live_unit_shows_name_owner_and_health()
    {
        var lines = HoverInfo.Lines("Rifle Squad", HoverInfo.OwnerLine(1, 0), 80, 120, lastSeen: false);
        Assert.Equal(new[] { "Rifle Squad", "Owner: player 2", "Health 80/120" }, lines);
    }

    [Fact]
    public void A_last_seen_building_says_so_and_a_building_without_max_health_shows_only_the_value()
    {
        var ghost = HoverInfo.Lines("Barracks", HoverInfo.OwnerLine(1, 0), 300, 500, lastSeen: true);
        Assert.Equal("Last seen, health 300/500", ghost[2]);
        Assert.Equal("Health 7", HoverInfo.Lines("Wall", "Owner: you", 7, 0, lastSeen: false)[2]);
    }

    [Fact]
    public void The_local_player_is_called_you()
    {
        Assert.Equal("Owner: you", HoverInfo.OwnerLine(0, 0));
        Assert.Equal(500, HoverInfo.DwellMs);
    }
}
