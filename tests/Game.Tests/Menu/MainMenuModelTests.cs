// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;

namespace Rts.Game.Tests.Menu;

public sealed class MainMenuModelTests
{
    [Fact]
    public void Entries_are_in_the_requested_order_and_have_no_sandbox()
    {
        IReadOnlyList<MainMenuEntry> entries = MainMenuModel.Build(hasSave: false);

        Assert.Equal(
            new[] { "Continue", "Campaign", "Skirmish", "LAN Game", "Map Editor", "Replays", "Archive", "How to Play", "Settings", "Quit" },
            entries.Select(e => e.Label));
        Assert.DoesNotContain(entries, e => e.Label.Contains("Sandbox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Continue_is_disabled_without_a_save_and_enabled_with_one()
    {
        MainMenuEntry none = MainMenuModel.Build(false)[0];
        MainMenuEntry some = MainMenuModel.Build(true)[0];

        Assert.Equal(MenuAction.Continue, none.Action);
        Assert.False(none.Enabled);
        Assert.Equal(MainMenuModel.NoSaves, none.Tooltip);
        Assert.True(some.Enabled);
        Assert.Null(some.Tooltip);
    }

    [Fact]
    public void Lan_is_disabled_with_the_coming_later_tooltip()
    {
        foreach (bool hasSave in new[] { false, true })
        {
            MainMenuEntry entry = MainMenuModel.Build(hasSave).Single(e => e.Action == MenuAction.LanGame);
            Assert.False(entry.Enabled);
            Assert.Equal("Coming in a later update", entry.Tooltip);
        }
    }

    [Fact]
    public void Map_editor_is_enabled_without_a_tooltip()
    {
        foreach (bool hasSave in new[] { false, true })
        {
            MainMenuEntry entry = MainMenuModel.Build(hasSave).Single(e => e.Action == MenuAction.MapEditor);
            Assert.True(entry.Enabled);
            Assert.Null(entry.Tooltip);
        }
    }

    [Fact]
    public void Every_other_entry_is_enabled()
    {
        var alwaysOff = new[] { MenuAction.Continue, MenuAction.LanGame };
        foreach (MainMenuEntry e in MainMenuModel.Build(true).Where(e => !alwaysOff.Contains(e.Action)))
            Assert.True(e.Enabled, e.Label);
        Assert.True(MainMenuModel.Build(true)[0].Enabled);
    }

    [Fact]
    public void Initial_selection_is_continue_when_usable_otherwise_campaign()
    {
        Assert.Equal(0, MainMenuModel.InitialSelection(MainMenuModel.Build(true)));
        Assert.Equal(1, MainMenuModel.InitialSelection(MainMenuModel.Build(false)));
    }

    [Fact]
    public void Move_wraps_both_ways()
    {
        Assert.Equal(0, MainMenuModel.Move(9, 1, 10));
        Assert.Equal(9, MainMenuModel.Move(0, -1, 10));
        Assert.Equal(4, MainMenuModel.Move(3, 1, 10));
    }

    private static InputFrame Press(Keys key) =>
        new(new KeyboardState(key), new KeyboardState(), new MouseState(), new MouseState());

    [Fact]
    public void Enter_on_an_enabled_row_returns_its_action_and_on_a_disabled_row_only_denies()
    {
        var lobby = new LobbyMenuController(MainMenuModel.Build(false));
        Assert.Equal(1, lobby.Selected);

        LobbyResult go = lobby.Update(Press(Keys.Enter), 1280, 720, 2);
        Assert.Equal(MenuAction.Campaign, go.Action);
        Assert.Equal(MenuSounds.Select, go.SoundId);

        lobby.Update(Press(Keys.Down), 1280, 720, 2);   // Skirmish
        lobby.Update(Press(Keys.Down), 1280, 720, 2);   // LAN Game
        Assert.Equal(MenuAction.LanGame, lobby.Entries[lobby.Selected].Action);
        LobbyResult denied = lobby.Update(Press(Keys.Enter), 1280, 720, 2);
        Assert.Null(denied.Action);
        Assert.Equal(MenuSounds.Deny, denied.SoundId);
    }

    [Fact]
    public void Disabled_continue_cannot_be_activated_with_the_keyboard()
    {
        var lobby = new LobbyMenuController(MainMenuModel.Build(false));
        lobby.Update(Press(Keys.Up), 1280, 720, 2);
        Assert.Equal(MenuAction.Continue, lobby.Entries[lobby.Selected].Action);

        Assert.Null(lobby.Update(Press(Keys.Enter), 1280, 720, 2).Action);
    }

    [Fact]
    public void Layout_gives_every_entry_a_row_that_fits_the_window()
    {
        foreach ((int w, int h, int scale) in new[] { (1280, 720, 2), (1920, 1080, 3), (1024, 600, 2) })
        {
            LobbyLayout layout = LobbyMenuView.Measure(w, h, scale, MainMenuModel.Build(true));
            Assert.Equal(10, layout.Rows.Length);
            Assert.All(layout.Rows, r => Assert.True(r.Top >= 0 && r.Bottom <= h && r.Right <= w, $"{w}x{h}: {r}"));
            for (int i = 1; i < layout.Rows.Length; i++) Assert.True(layout.Rows[i].Top >= layout.Rows[i - 1].Bottom);
        }
    }

    [Fact]
    public void Tooltip_is_drawn_only_for_the_selected_disabled_row()
    {
        IReadOnlyList<MainMenuEntry> entries = MainMenuModel.Build(false);
        LobbyLayout layout = LobbyMenuView.Measure(1280, 720, 2, entries);

        var on = new List<DrawOp>();
        LobbyMenuView.Build(on, layout, entries, selected: 3, 0f, "", "0", 1280, 720);
        var off = new List<DrawOp>();
        LobbyMenuView.Build(off, layout, entries, selected: 1, 0f, "", "0", 1280, 720);

        Assert.Contains(on, o => o.Kind == DrawOpKind.Text && o.Id == "Coming in a later update");
        Assert.DoesNotContain(off, o => o.Kind == DrawOpKind.Text && o.Id == "Coming in a later update");
    }
}
