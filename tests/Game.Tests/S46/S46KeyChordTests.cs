// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Screens;

namespace Rts.Game.Tests.S46;

[Collection("UiStatics")]   // BoundInput reads the process-wide GameKeys.Active
public sealed class S46KeyChordTests
{
    [Theory]
    [InlineData("D1", Keys.D1, false)]
    [InlineData("Alt+D1", Keys.D1, true)]
    [InlineData("alt+d1", Keys.D1, true)]
    [InlineData("NumPad7", Keys.NumPad7, false)]
    [InlineData("Alt+OemMinus", Keys.OemMinus, true)]
    public void Chord_parses_and_round_trips(string text, Keys key, bool alt)
    {
        Assert.True(KeyChord.TryParse(text, out KeyChord c));
        Assert.Equal(new KeyChord(key, alt), c);
        Assert.True(KeyChord.TryParse(c.ToSetting(), out KeyChord again));
        Assert.Equal(c, again);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Alt+")]
    [InlineData("Banana")]
    [InlineData("Alt+Banana")]
    [InlineData("12345")]
    [InlineData("Ctrl+D1")]
    public void Junk_does_not_parse(string text) => Assert.False(KeyChord.TryParse(text, out _));

    [Fact]
    public void Chord_text_and_names()
    {
        Assert.Equal("D1", KeyChord.Of(Keys.D1).ToSetting());
        Assert.Equal("Alt+D1", new KeyChord(Keys.D1, true).ToSetting());
        Assert.Equal("Alt+1", KeyNames.Of(new KeyChord(Keys.D1, true)));
        Assert.Equal("Num 7", KeyNames.Of(KeyChord.Of(Keys.NumPad7)));
        Assert.True(KeyChord.None.IsNone);
    }

    [Fact]
    public void Default_is_unchanged_and_cameos_stay_on_the_numpad()
    {
        Assert.Equal(KeyChord.Of(Keys.NumPad7), KeyBindings.Default.GetChord(GameAction.CameoSlot1));
        Assert.Equal(Keys.NumPad7, KeyBindings.Default.Get(GameAction.CameoSlot1));
        Assert.True(KeyBindings.Default.IsAllDefault);
        Assert.Empty(KeyBindings.Default.Overrides());
        Assert.Empty(KeyBindings.Default.Conflicts());
        foreach (ActionInfo info in GameKeys.Infos)
        {
            Assert.Equal(KeyChord.Of(info.Default), KeyBindings.Default.GetChord(info.Action));
            Assert.True(KeyBindings.Default.GetAltChord(info.Action).IsNone);
        }
    }

    [Fact]
    public void Laptop_has_no_conflicts_and_the_expected_cameo_chords()
    {
        KeyBindings laptop = KeyBindings.Laptop;
        Assert.Empty(laptop.Conflicts());
        Keys[] expected = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0, Keys.OemMinus, Keys.OemPlus };
        for (int i = 0; i < 12; i++)
        {
            GameAction a = GameAction.CameoSlot1 + i;
            Assert.Equal($"cameo_slot_{i + 1}", GameKeys.InfoOf(a).Id);
            Assert.Equal(new KeyChord(expected[i], true), laptop.GetChord(a));
            Assert.True(laptop.GetAltChord(a).IsNone);
        }

        Assert.False(laptop.IsAllDefault);
        Assert.Equal(KeyBindings.Default.GetChord(GameAction.GameSpeedUp), laptop.GetChord(GameAction.GameSpeedUp));   // bare = still speed up
    }

    [Fact]
    public void Alt_digit_does_not_clash_with_the_bare_digit()
    {
        KeyBindings b = KeyBindings.Default.With(GameAction.CameoSlot1, new KeyChord(Keys.D1, true));
        Assert.Null(b.ConflictOf(GameAction.CameoSlot1, new KeyChord(Keys.D1, true)));
        Assert.Equal(KeyChord.Of(Keys.D1), b.GetChord(GameAction.Group1));
        Assert.Empty(b.Conflicts());                                                                       // Alt+1 (cameo) beside 1 (group 1)
        Assert.Equal(GameAction.Group1, b.ConflictOf(GameAction.Pack, Keys.D1));                          // bare 1 is Group1's
        Assert.Equal(GameAction.CameoSlot1, b.ConflictOf(GameAction.Pack, new KeyChord(Keys.D1, true)));  // Alt+1 is the cameo's only
        Assert.NotEmpty(b.With(GameAction.CameoSlot1, Keys.D1).Conflicts());                               // bare 1 on the cameo would clash
        Assert.NotEmpty(KeyBindings.Default.With(GameAction.Group1, new KeyChord(Keys.D1, true)).With(GameAction.CameoSlot1, new KeyChord(Keys.D1, true)).Conflicts());
    }

    [Fact]
    public void FromOverrides_reads_chords_and_old_bare_names()
    {
        KeyBindings b = KeyBindings.FromOverrides(new[] { ("cameo_slot_1", "Alt+D1") });
        Assert.Equal(new KeyChord(Keys.D1, true), b.GetChord(GameAction.CameoSlot1));
        Assert.Equal(Keys.D1, b.Get(GameAction.CameoSlot1));

        KeyBindings old = KeyBindings.FromOverrides(new[] { ("stop", "V"), ("cameo_slot_2", "oembackslash") });
        Assert.Equal(KeyChord.Of(Keys.V), old.GetChord(GameAction.Stop));
        Assert.Equal(KeyChord.Of(Keys.OemBackslash), old.GetChord(GameAction.CameoSlot2));

        KeyBindings junk = KeyBindings.FromOverrides(new[] { ("stop", "Alt+Nope"), ("stop", "Alt+Escape") });
        Assert.True(junk.IsAllDefault);
    }

    [Fact]
    public void Laptop_overrides_round_trip()
    {
        KeyBindings back = KeyBindings.FromOverrides(KeyBindings.Laptop.Overrides(), KeyBindings.Laptop.AltOverrides());
        Assert.Equal(KeyBindings.Laptop, back);
        Assert.Equal(KeyBindings.Laptop.GetHashCode(), back.GetHashCode());
        Assert.Contains(("cameo_slot_1", "Alt+D1"), KeyBindings.Laptop.Overrides());
        Assert.Contains(("cameo_slot_12", "Alt+OemPlus"), KeyBindings.Laptop.Overrides());

        KeyBindings withSecond = KeyBindings.Default.WithAlt(GameAction.Stop, new KeyChord(Keys.D5, true));
        KeyBindings second = KeyBindings.FromOverrides(withSecond.Overrides(), withSecond.AltOverrides());
        Assert.Equal(withSecond, second);
        Assert.Equal(new KeyChord(Keys.D5, true), second.GetAltChord(GameAction.Stop));
    }

    [Fact]
    public void Settings_file_round_trips_the_laptop_preset()
    {
        Settings s = ControlsScreen.ApplyLaptopKeys(Settings.Default);
        Assert.Equal(KeyBindings.Laptop, s.Bindings);
        string text = s.ToToml();
        Assert.Contains("cameo_slot_1 = \"Alt+D1\"", text);
        Assert.Equal(KeyBindings.Laptop, Settings.Parse(text).Bindings);
    }

    private static InputFrame Frame(bool alt, params Keys[] pressedNow)
    {
        var down = new List<Keys>(pressedNow);
        if (alt) down.Add(Keys.LeftAlt);
        return new InputFrame(new KeyboardState(down.ToArray()), new KeyboardState(), new MouseState(), new MouseState());
    }

    [Fact]
    public void BoundInput_matches_alt_chords_only_with_alt_held()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Laptop;   // bare 1 = Group1, Alt+1 = cameo 1
            Assert.True(Frame(true, Keys.D1).Pressed(GameAction.CameoSlot1));
            Assert.True(Frame(true, Keys.D1).IsDown(GameAction.CameoSlot1));
            Assert.False(Frame(false, Keys.D1).Pressed(GameAction.CameoSlot1));
            Assert.False(Frame(false, Keys.D1).IsDown(GameAction.CameoSlot1));

            Assert.True(Frame(false, Keys.D1).Pressed(GameAction.Group1));
            Assert.False(Frame(true, Keys.D1).Pressed(GameAction.Group1));

            InputFrame alt = Frame(true, Keys.D1);
            Assert.True(alt.ConsumePressed(GameAction.CameoSlot1));
            Assert.False(alt.ConsumePressed(GameAction.CameoSlot1));
            InputFrame bare = Frame(false, Keys.D1);
            Assert.False(bare.ConsumePressed(GameAction.CameoSlot1));
            Assert.True(bare.ConsumePressed(GameAction.Group1));
        }
        finally { GameKeys.Active = saved; }
    }

    [Fact]
    public void BoundInput_ignores_shift_and_ctrl_and_consumes_both_keys()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Default.With(GameAction.Stop, new KeyChord(Keys.D1, true)).With(GameAction.Group1, Keys.F2).WithAlt(GameAction.Stop, Keys.V);
            Assert.True(Frame(true, Keys.D1, Keys.LeftShift, Keys.LeftControl).Pressed(GameAction.Stop));
            Assert.True(Frame(false, Keys.V, Keys.LeftShift).Pressed(GameAction.Stop));
            Assert.False(Frame(true, Keys.V).Pressed(GameAction.Stop));   // second key is bare: Alt held spoils it

            InputFrame both = Frame(true, Keys.D1, Keys.V);   // Alt+1 matches; V (bare) does not, and is not consumed
            Assert.True(both.ConsumePressed(GameAction.Stop));
            Assert.True(both.Pressed(Keys.V));
            InputFrame bothBare = Frame(false, Keys.D1, Keys.V);
            Assert.True(bothBare.ConsumePressed(GameAction.Stop));
            Assert.False(bothBare.Pressed(Keys.V));
        }
        finally { GameKeys.Active = saved; }
    }

    [Fact]
    public void Rebind_with_alt_binds_the_chord_and_a_lone_alt_is_refused()
    {
        var session = new RebindSession();
        session.Begin(GameAction.CameoSlot1);
        Assert.Null(session.Press(KeyBindings.Default, Keys.LeftAlt, alt: true));
        Assert.Equal(RebindSession.Phase.Listening, session.State);

        KeyBindings? bound = session.Press(KeyBindings.Default, Keys.D1, alt: true);
        Assert.NotNull(bound);
        Assert.Equal(new KeyChord(Keys.D1, true), bound!.GetChord(GameAction.CameoSlot1));
        Assert.Equal(RebindSession.Phase.Idle, session.State);

        session.Begin(GameAction.CameoSlot2, second: true);
        KeyBindings? second = session.Press(bound, Keys.D2, alt: true);
        Assert.Equal(new KeyChord(Keys.D2, true), second!.GetAltChord(GameAction.CameoSlot2));

        // a taken chord still goes to the conflict flow, and swapping keeps the chords whole
        session.Begin(GameAction.CameoSlot3);
        Assert.Null(session.Press(bound, Keys.D1, alt: true));
        Assert.Equal(RebindSession.Phase.Conflict, session.State);
        Assert.Equal(GameAction.CameoSlot1, session.ConflictWith);
        KeyBindings? swapped = session.Swap(bound);
        Assert.Equal(new KeyChord(Keys.D1, true), swapped!.GetChord(GameAction.CameoSlot3));
        Assert.Equal(KeyChord.Of(GameKeys.InfoOf(GameAction.CameoSlot3).Default), swapped.GetChord(GameAction.CameoSlot1));

        // bare 1 is free beside Alt+1
        session.Begin(GameAction.CameoSlot3);
        Assert.NotNull(session.Press(bound, Keys.V));
    }

    [Fact]
    public void Controls_page_has_the_laptop_item_after_reset()
    {
        var items = ControlsScreen.Items();
        int reset = items.ToList().FindIndex(i => i.Kind == ControlsScreen.ItemKind.Reset);
        Assert.Equal(ControlsScreen.ItemKind.Laptop, items[reset + 1].Kind);
        Assert.True(items[reset + 1].Selectable);
        Assert.Equal("Laptop keys: sidebar cameos on Alt + 1-9, 0, -, =", ControlsScreen.LaptopText);
    }

    [Fact]
    public void Cameo_hint_shows_the_chord()
    {
        KeyBindings saved = GameKeys.Active;
        try
        {
            GameKeys.Active = KeyBindings.Laptop;
            Assert.Equal("Alt+1", Rts.Game.Ui.Sidebar.CameoHotkeys.KeyName(0));
            GameKeys.Active = KeyBindings.Default;
            Assert.Equal("Num 7", Rts.Game.Ui.Sidebar.CameoHotkeys.KeyName(0));
        }
        finally { GameKeys.Active = saved; }
    }
}
