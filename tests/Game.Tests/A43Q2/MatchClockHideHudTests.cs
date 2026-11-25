// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Linq;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Xunit;

namespace Rts.Game.Tests.A43Q2;

public sealed class MatchClockHideHudTests
{
    [Fact]
    public void The_match_clock_reads_minutes_and_seconds_then_hours()
    {
        int tps = SimConstants.TicksPerSecond;
        Assert.Equal("00:00", MatchClock.Format(0));
        Assert.Equal("14:32", MatchClock.Format(872 * tps + tps - 1));
        Assert.Equal("1:05:09", MatchClock.Format((3600 + 5 * 60 + 9) * tps));
    }

    [Fact]
    public void The_cameo_archive_key_is_the_apostrophe_and_free()
    {
        Assert.Equal(GameAction.CameoArchive, GameKeys.Infos[(int)GameAction.CameoArchive].Action);
        Assert.Equal(Keys.OemQuotes, GameKeys.InfoOf(GameAction.CameoArchive).Default);
        Assert.Equal(1, GameKeys.Infos.Count(i => i.Default == Keys.OemQuotes));
        Assert.Empty(KeyBindings.Default.Conflicts());
    }

    [Fact]
    public void The_hide_hud_action_has_its_own_row_in_the_table_order_and_a_free_default_key()
    {
        Assert.Equal(GameAction.HideHud, GameKeys.Infos[(int)GameAction.HideHud].Action);
        Assert.Equal(Keys.F11, GameKeys.InfoOf(GameAction.HideHud).Default);
        Assert.Equal(1, GameKeys.Infos.Count(i => i.Default == Keys.F11));
        Assert.Empty(KeyBindings.Default.Conflicts());
    }
}
