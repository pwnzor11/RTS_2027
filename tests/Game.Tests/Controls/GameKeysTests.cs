// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Controls;

public sealed class GameKeysTests
{
    // The files that read the keyboard during play. They may name a literal Keys.X only for the fixed keys (GameKeys.FixedKeyNames);
    // everything else must be a GameAction read through the bindings.
    private static readonly string[] InputSources =
    {
        "src/Game/Screens/GameScreen.cs",
        "src/Game/Ui/Sidebar/SidebarPanel.cs",
        "src/Game/Controls/OrderController.cs",
        "src/Game/Ui/Spectator/SpectatorController.cs",
        "src/Game/Ui/Llm/CommanderChatPanel.cs",
        "src/Game/Ui/Objectives/ObjectiveGuidance.cs",
    };

    private static string Root()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repository root not found");
    }

    private static string AllSource() => string.Join("\n", InputSources.Select(f => File.ReadAllText(Path.Combine(Root(), f))));

    [Fact]
    public void Input_code_names_no_literal_key_except_the_fixed_ones()
    {
        var found = new HashSet<string>();
        foreach (Match m in Regex.Matches(AllSource(), @"(?<![A-Za-z])Keys\.([A-Za-z0-9]+)")) found.Add(m.Groups[1].Value);
        string[] loose = found.Where(k => !GameKeys.FixedKeyNames.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.True(loose.Length == 0, "literal keys in input code (use a GameAction via GameKeys.Infos): " + string.Join(", ", loose));
    }

    [Fact]
    public void Every_action_is_read_by_the_input_code()
    {
        string source = AllSource();
        string[] unread = Enum.GetValues<GameAction>().Select(a => a.ToString()).Where(a => !Regex.IsMatch(source, @"GameAction\." + a + @"\b")).ToArray();
        Assert.True(unread.Length == 0, "actions in GameKeys.Infos that no input code reads: " + string.Join(", ", unread));
    }

    [Fact]
    public void Infos_cover_every_action_once_in_enum_order_with_unique_ids()
    {
        GameAction[] all = Enum.GetValues<GameAction>();
        Assert.Equal(all.Length, GameKeys.Infos.Count);
        for (int i = 0; i < all.Length; i++) Assert.Equal(all[i], GameKeys.Infos[i].Action);
        Assert.Equal(all.Length, GameKeys.Infos.Select(i => i.Id).Distinct().Count());
        Assert.All(GameKeys.Infos, i => Assert.Contains(i.Group, GameKeys.Groups));
    }

    [Fact]
    public void Default_bindings_have_no_conflict_and_only_bindable_keys()
    {
        Assert.Empty(KeyBindings.Default.Conflicts());
        Assert.All(GameKeys.Infos, i => Assert.True(KeyBindings.IsBindable(i.Default), i.Id));
    }

    [Fact]
    public void Groups_are_in_order_and_each_has_rows()
    {
        Assert.Equal(new[] { "Camera", "Selection", "Orders", "Build & System" }, GameKeys.Groups);
        foreach (string g in GameKeys.Groups) Assert.NotEmpty(GameKeys.InGroup(g));
        Assert.All(GameKeys.Rows, r => Assert.Contains(r.Group, GameKeys.Groups));
        Assert.Equal(GameKeys.Infos.Count, GameKeys.Rows.Count(r => r.Action is not null));   // every action has a line on the page
    }

    [Fact]
    public void Order_actions_are_Orders_rows()
    {
        foreach (GameAction a in GameKeys.OrderActions) Assert.Equal(GameKeys.Orders, GameKeys.InfoOf(a).Group);
        Assert.Equal(Keys.K, GameKeys.InfoOf(GameAction.Pack).Default);
    }

    [Fact]
    public void Debug_keys_are_off_in_skirmish_and_campaign()
    {
        Assert.False(GameKeys.DebugKeysAllowed(skirmishOrMission: true));
        Assert.True(GameKeys.DebugKeysAllowed(skirmishOrMission: false));
    }

    [Fact]
    public void Ranges_overlay_is_on_F10_and_bookmarks_on_F6_to_F8_and_F12()
    {
        Assert.Equal(Keys.F10, GameKeys.InfoOf(GameAction.DebugRanges).Default);
        Assert.Equal(Keys.Home, GameKeys.InfoOf(GameAction.CentreBase).Default);
        Assert.Equal(new[] { Keys.F6, Keys.F7, Keys.F8, Keys.F12 },
            new[] { GameAction.Bookmark1, GameAction.Bookmark2, GameAction.Bookmark3, GameAction.Bookmark4 }.Select(a => GameKeys.InfoOf(a).Default));
    }

    [Fact]
    public void Controls_page_lists_every_group_setting_and_row()
    {
        var items = ControlsScreen.Items();
        Assert.Equal(3 + 1 + GameKeys.Groups.Count + GameKeys.Rows.Count, items.Count);
        foreach (string g in GameKeys.Groups) Assert.Contains(items, i => i.Heading == g);
        Assert.Equal(GameKeys.Infos.Count, items.Count(i => i.Kind == ControlsScreen.ItemKind.Binding));
    }

    [Fact]
    public void Controls_page_settings_step_and_clamp()
    {
        Settings s = Settings.Default;
        Assert.False(ControlsScreen.Step(s, 0, 1).EdgeScroll);
        Assert.True(ControlsScreen.Step(s, 2, -1).ShowDiagnostics);
        Assert.Equal(Settings.DefaultScrollSpeed + 100, ControlsScreen.Step(s, 1, 1).ScrollSpeed);
        Assert.Equal(Settings.MinScrollSpeed, ControlsScreen.Step(s with { ScrollSpeed = Settings.MinScrollSpeed }, 1, -1).ScrollSpeed);
        Assert.Equal(Settings.MaxScrollSpeed, ControlsScreen.Step(s with { ScrollSpeed = Settings.MaxScrollSpeed }, 1, 1).ScrollSpeed);
        Assert.Contains("Off", ControlsScreen.RowText(s, 2));
    }
}
