// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Controls;

public sealed class GameKeysTests
{
    // The files that read the keyboard during play. Escape, Back and Length are not bindings of their own (Esc is in the table;
    // Back edits chat text; "Keys.Length" is an array length, not a key).
    private static readonly string[] InputSources =
    {
        "src/Game/Screens/GameScreen.cs",
        "src/Game/Ui/Sidebar/SidebarPanel.cs",
        "src/Game/Controls/OrderController.cs",
        "src/Game/Ui/Spectator/SpectatorController.cs",
        "src/Game/Ui/Llm/CommanderChatPanel.cs",
    };

    private static readonly HashSet<string> NotBindings = new() { "Back", "Length" };

    private static string Root()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) dir = Path.GetDirectoryName(dir);
        return dir ?? throw new InvalidOperationException("repository root not found");
    }

    private static HashSet<string> KeysInCode()
    {
        var found = new HashSet<string>();
        foreach (string file in InputSources)
        {
            string text = File.ReadAllText(Path.Combine(Root(), file));
            foreach (Match m in Regex.Matches(text, @"(?<![A-Za-z])Keys\.([A-Za-z0-9]+)"))
                found.Add(m.Groups[1].Value);
        }

        return found;
    }

    [Fact]
    public void Every_key_the_input_code_reads_is_in_the_table()
    {
        var table = new HashSet<string>(GameKeys.AllKeys.Select(k => k.ToString()));
        string[] missing = KeysInCode().Where(k => !table.Contains(k) && !NotBindings.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.True(missing.Length == 0, "keys handled in code but missing from GameKeys.Table: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_key_in_the_table_is_read_by_the_input_code()
    {
        HashSet<string> code = KeysInCode();
        string[] stale = GameKeys.AllKeys.Select(k => k.ToString()).Where(k => !code.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.True(stale.Length == 0, "keys listed in GameKeys.Table that no input code reads: " + string.Join(", ", stale));
    }

    [Fact]
    public void Groups_are_in_order_and_each_has_rows()
    {
        Assert.Equal(new[] { "Camera", "Selection", "Orders", "Build & System" }, GameKeys.Groups);
        foreach (string g in GameKeys.Groups) Assert.NotEmpty(GameKeys.InGroup(g));
        Assert.All(GameKeys.Table, b => Assert.Contains(b.Group, GameKeys.Groups));
    }

    [Fact]
    public void Order_keys_are_exactly_the_Orders_rows_the_order_controller_handles()
    {
        var orderRows = GameKeys.InGroup(GameKeys.Orders).SelectMany(b => b.Handled).ToHashSet();
        foreach (Keys k in GameKeys.OrderKeys) Assert.Contains(k, orderRows);
        Assert.Contains(Keys.K, orderRows);   // K is read by GameScreen's pack handling, not the order controller
    }

    [Fact]
    public void Debug_keys_are_off_in_skirmish_and_campaign()
    {
        Assert.False(GameKeys.DebugKeysAllowed(skirmishOrMission: true));
        Assert.True(GameKeys.DebugKeysAllowed(skirmishOrMission: false));
    }

    [Fact]
    public void Controls_page_lists_every_group_and_every_row()
    {
        var rows = ControlsScreen.TableRows();
        Assert.Equal(GameKeys.Groups.Count + GameKeys.Table.Count, rows.Count);
        foreach (string g in GameKeys.Groups) Assert.Contains(rows, r => r.Heading == g);
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
