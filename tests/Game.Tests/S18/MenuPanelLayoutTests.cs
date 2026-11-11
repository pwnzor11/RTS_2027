// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S18;

/// <summary>S18 UX-LM-02 / UX-LM-03 / UX-LM-12: the setup-style panel fits the window at every scale and text size, keeps one width, never
/// draws a label under the swatch or the buttons, and information lines are not choices.</summary>
[Collection("UiStatics")]
public sealed class MenuPanelLayoutTests
{
    private static readonly string[] Tiers = { "Recruit", "Regular", "Veteran", "Expert", "Elite" };

    private static InputFrame Idle() => new(new KeyboardState(), new KeyboardState(), new MouseState(), new MouseState());

    private static InputFrame Key(Keys k) => new(new KeyboardState(k), new KeyboardState(), new MouseState(), new MouseState());

    private static void WithText(TextSize size, Action body)
    {
        float before = UiScale.TextFactor;
        try
        {
            UiScale.TextFactor = UiScale.FactorOf(size);
            body();
        }
        finally
        {
            UiScale.TextFactor = before;
        }
    }

    // A panel shaped like the skirmish setup page: a map row, a slot count, four slot rows (label, swatch, six buttons), a hint, Start, Back.
    private static (MenuPanel Panel, SkirmishSlot[] Slots) SetupLikePanel()
    {
        var slots = new[]
        {
            new SkirmishSlot(SlotKind.Human, Faction.Authority, 3, 0, 0),
            new SkirmishSlot(SlotKind.Ai, Faction.Directorate, 3, 1, 1, Commander: true),
            new SkirmishSlot(SlotKind.Ai, Faction.Expedition, 5, 2, 2, Commander: true),
            new SkirmishSlot(SlotKind.Closed, Faction.Authority, 3, 3, 3),
        };
        var rows = new List<MenuItem>
        {
            new(() => "Map: rustbelt_m2 64x64, 2 players (curated)", null, null, new MenuButton("<", () => { }), new MenuButton(">", () => { })),
            new(() => $"Slots: {slots.Length}", null, null, new MenuButton("-", () => { }), new MenuButton("+", () => { })),
        };
        for (int i = 0; i < slots.Length; i++)
        {
            int slot = i;
            rows.Add(new(() => SkirmishSetupScreen.SlotLabel(slots[slot], slot, Tiers), null, null,
                new MenuButton("Kind", () => { }), new MenuButton("Fac", () => { }), new MenuButton("Tier", () => { }),
                new MenuButton("Cmdr", () => { }), new MenuButton("Team", () => { }), new MenuButton("Col", () => { }))
            {
                WidthTexts = SkirmishSetupScreen.WidestSlotTexts(Tiers),
                Swatch = () => slots[slot].Kind == SlotKind.Closed ? null : Color.Orange,
            });
        }
        rows.Add(new(() => "(Kind Fac Tier Cmdr Team Col cycle each choice)") { Info = true });
        rows.Add(new(() => "Start", () => { }));
        rows.Add(new(() => "Back", () => { }));
        return (new MenuPanel("Skirmish", rows), slots);
    }

    public static IEnumerable<object[]> Windows()
    {
        foreach (TextSize size in Enum.GetValues<TextSize>())
            foreach (int percent in new[] { 100, 150, 200 })
                foreach ((int w, int h) in new[] { (1280, 720), (1366, 768), (1024, 768) })
                    yield return new object[] { size, percent, w, h };
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public void The_setup_panel_fits_the_window_at_every_scale_and_text_size(TextSize size, int percent, int w, int h) => WithText(size, () =>
    {
        UiUnit unit = UiUnit.FromScale(UiScale.Resolve(w, h, percent).Scale);
        (MenuPanel panel, _) = SetupLikePanel();
        panel.Update(Idle(), new Viewport(0, 0, w, h), unit);
        var window = new Rectangle(0, 0, w, h);
        Assert.True(window.Contains(panel.PanelRect), $"panel {panel.PanelRect} outside {w}x{h} ({size}, {percent}%)");
        for (int i = 0; i < 9; i++)
        {
            Assert.True(panel.PanelRect.Contains(panel.RowRect(i)), $"row {i} outside the panel ({size}, {percent}%, {w}x{h})");
            if (i is >= 2 and <= 5)
            {
                Rectangle last = panel.ButtonRect(i, 5);
                Assert.True(panel.RowRect(i).Contains(last), $"row {i}: the Col button leaves its row ({size}, {percent}%, {w}x{h})");
            }
        }
    });

    [Theory]
    [MemberData(nameof(Windows))]
    public void A_slot_label_never_runs_under_the_swatch_or_the_buttons(TextSize size, int percent, int w, int h) => WithText(size, () =>
    {
        UiUnit unit = UiUnit.FromScale(UiScale.Resolve(w, h, percent).Scale);
        (MenuPanel panel, _) = SetupLikePanel();
        panel.Update(Idle(), new Viewport(0, 0, w, h), unit);
        // The panel shrinks itself to fit, so measure at the scale it settled on: the label column is what it reports.
        for (int row = 2; row <= 5; row++)
        {
            Rectangle text = panel.TextRect(row), swatch = panel.SwatchRect(row), first = panel.ButtonRect(row, 0);
            Assert.True(text.Right <= swatch.X, $"row {row}: label column {text} reaches the swatch {swatch} ({size}, {percent}%, {w}x{h})");
            Assert.True(swatch.Right <= first.X, $"row {row}: swatch {swatch} overlaps the Kind button {first}");
            for (int b = 1; b < 6; b++) Assert.True(panel.ButtonRect(row, b - 1).Right <= panel.ButtonRect(row, b).X, $"row {row}: buttons {b - 1} and {b} overlap");
            Assert.True(text.Width > 0, $"row {row}: no room left for the label");
        }
    });

    [Fact]
    public void An_over_long_label_is_cut_with_an_ellipsis_instead_of_drawn_under_the_buttons()
    {
        var rows = new List<MenuItem>
        {
            new(() => "3 AI LLM - Expert Overseas Expedition T4 (no key - plays as standard AI) and then some more words", null, null,
                new MenuButton("Kind", () => { }), new MenuButton("Col", () => { })) { Swatch = () => Color.Red },
        };
        var panel = new MenuPanel("T", rows);
        panel.Update(Idle(), new Viewport(0, 0, 400, 300), 2);   // a small window: the panel shrinks, the long label still has to be cut
        string shown = panel.ShownText(0);
        Assert.EndsWith("...", shown);
        Assert.True(BitmapFont.Measure(shown, panel.LayoutScale) <= panel.TextRect(0).Width, "the shown text is wider than its column");
    }

    [Fact]
    public void The_panel_keeps_one_width_while_a_choice_changes_its_text()
    {
        string[] labels = { "Map: a", "Map: rustbelt_m2 64x64, 2 players (curated)", "Map: Generated" };
        int pick = 0;
        var wide = new MenuItem(() => labels[pick], null, null, new MenuButton("<", () => { }), new MenuButton(">", () => { })) { WidthTexts = labels };
        var narrow = new MenuItem(() => labels[pick], null, null, new MenuButton("<", () => { }), new MenuButton(">", () => { }));
        var withWidths = new MenuPanel("T", new[] { wide });
        var without = new MenuPanel("T", new[] { narrow });
        var vp = new Viewport(0, 0, 1280, 720);
        var widths = new List<int>();
        var unguided = new List<int>();
        for (pick = 0; pick < labels.Length; pick++)
        {
            withWidths.Update(Idle(), vp, 2);
            without.Update(Idle(), vp, 2);
            widths.Add(withWidths.PanelRect.Width);
            unguided.Add(without.PanelRect.Width);
        }
        Assert.Single(widths.Distinct());                // with WidthTexts the panel never moves under the cursor
        Assert.True(unguided.Distinct().Count() > 1);    // and this is what it did before: the width followed the text
    }

    [Fact]
    public void Information_lines_are_skipped_by_the_keys_and_ignored_by_the_mouse()
    {
        var log = new List<string>();
        var rows = new List<MenuItem>
        {
            new(() => "One", () => log.Add("one")),
            new(() => "Help text") { Info = true },
            new(() => "Two", () => log.Add("two")),
            new(() => "Footer") { Info = true },
        };
        var panel = new MenuPanel("T", rows);
        var vp = new Viewport(0, 0, 1280, 720);
        panel.Update(Idle(), vp, 2);
        Assert.Equal(0, panel.Selected);
        panel.Update(Key(Keys.Down), vp, 2);
        Assert.Equal(2, panel.Selected);               // not the help line
        panel.Update(Key(Keys.Down), vp, 2);
        Assert.Equal(0, panel.Selected);               // wraps past the footer
        panel.Update(Key(Keys.Up), vp, 2);
        Assert.Equal(2, panel.Selected);

        Point onHelp = panel.RowRect(1).Center;
        var hover = new InputFrame(new KeyboardState(), new KeyboardState(), new MouseState(onHelp.X, onHelp.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released), new MouseState());
        panel.Update(hover, vp, 2);
        Assert.Equal(2, panel.Selected);               // hovering a help line selects nothing
    }

    [Fact]
    public void A_list_that_starts_on_an_information_line_moves_to_the_first_choice()
    {
        var rows = new List<MenuItem> { new(() => "No saved games yet.") { Info = true }, new(() => "Back", () => { }) };
        var panel = new MenuPanel("Load", rows);
        panel.Update(Idle(), new Viewport(0, 0, 1280, 720), 2);
        Assert.Equal(1, panel.Selected);
    }

    [Fact]
    public void The_list_screen_marks_the_empty_message_the_detail_and_the_hint_as_information()
    {
        var empty = new ListScreen(null!, "Load Campaign", Array.Empty<ListRow>(), "No saved games yet.", "Pause a game and choose Save game.");
        empty.Panel.Update(Idle(), new Viewport(0, 0, 1280, 720), 2);
        Assert.Equal(2, empty.Panel.Selected);          // Back: the empty message is not a choice

        var rows = new[] { new ListRow("save 1", "Skirmish", () => { }), new ListRow("save 2", "Skirmish", () => { }) };
        var full = new ListScreen(null!, "Load Skirmish", rows, "none", "hint");
        full.Panel.Update(Idle(), new Viewport(0, 0, 1280, 720), 2);
        full.Panel.Update(Key(Keys.Down), new Viewport(0, 0, 1280, 720), 2);   // save 2
        full.Panel.Update(Key(Keys.Down), new Viewport(0, 0, 1280, 720), 2);   // not the detail (2) or the hint (3): Back (4)
        Assert.Equal(4, full.Panel.Selected);
    }
}
