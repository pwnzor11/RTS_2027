// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Menu;
using Rts.Game.Ui;
using Rts.Game.Ui.Sidebar;

namespace Rts.Game.Tests.S45;

/// <summary>M16 UI correctness wave (U-01 caption from bindings, U-02 selection toasts, U-03 save-failure text).</summary>
[Collection("GameKeysActive")]   // touches the process-wide GameKeys.Active
public sealed class S45UiTests
{
    [Fact]
    public void U01_default_captions_read_exactly_as_before()
    {
        GameKeys.Active = KeyBindings.Default;
        Assert.Equal("Dive (D)", DiveOffer.Caption(false));
        Assert.Equal("Surface (D)", DiveOffer.Caption(true));
        Assert.Equal("Lay mine (D) 3 left", DiveOffer.MineCaption(3));
        Assert.Equal("PACK RIG (K)", LeftBehindText.Placeholder(LeftBehindText.PackKey));
        Assert.Equal("UNPACK RIG (K)", LeftBehindText.Placeholder(LeftBehindText.UnpackKey));
    }

    [Fact]
    public void U01_rebound_deploy_and_pack_keys_show_their_new_letter()
    {
        try
        {
            GameKeys.Active = KeyBindings.Default.With(GameAction.Deploy, Keys.F).With(GameAction.Pack, Keys.J);
            Assert.Equal("Dive (F)", DiveOffer.Caption(false));
            Assert.Equal("Surface (F)", DiveOffer.Caption(true));
            Assert.Equal("Lay mine (F) 3 left", DiveOffer.MineCaption(3));
            Assert.Equal("PACK RIG (J)", LeftBehindText.Placeholder(LeftBehindText.PackKey));
            Assert.Equal("UNPACK RIG (J)", LeftBehindText.Placeholder(LeftBehindText.UnpackKey));
        }
        finally
        {
            GameKeys.Active = KeyBindings.Default;
        }
    }

    [Fact]
    public void U02_group_and_army_messages()
    {
        Assert.Equal("No army units to select", SelectionFeedbackText.NoArmy());
        Assert.Equal("Group 3 set (5 units)", SelectionFeedbackText.GroupSet(3, 5));
        Assert.Equal("Group 1 set (1 unit)", SelectionFeedbackText.GroupSet(1, 1));
    }

    [Fact]
    public void U03_save_failure_reason_is_short_and_hides_the_raw_exception()
    {
        string io = SaveFailureText.Reason(new IOException("disk path C:\\saves\\secret.sav"));
        Assert.Equal("the save file could not be written", io);
        Assert.DoesNotContain("secret", io, StringComparison.Ordinal);

        Assert.Equal("access to the save folder was denied", SaveFailureText.Reason(new UnauthorizedAccessException("nope")));
    }

    [Fact]
    public void U03_long_toast_line_wraps_to_the_width()
    {
        const string text = "Quicksave failed: the save file could not be written because the folder is read only";
        List<string> lines = TextWrap.Wrap(text, 120, t => t.Length * 6);
        Assert.True(lines.Count > 1);
        foreach (string line in lines) Assert.True(line.Length * 6 <= 120, $"line too wide: {line}");
    }
}
