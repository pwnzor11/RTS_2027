// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Controls;

public sealed class KeyBindingsTests
{
    [Fact]
    public void Lookup_gives_the_default_key_until_changed()
    {
        Assert.Equal(Keys.A, KeyBindings.Default.Get(GameAction.AttackMove));
        KeyBindings b = KeyBindings.Default.With(GameAction.AttackMove, Keys.F);
        Assert.Equal(Keys.F, b.Get(GameAction.AttackMove));
        Assert.Equal(Keys.S, b.Get(GameAction.Stop));
        Assert.False(b.IsDefault(GameAction.AttackMove));
        Assert.Equal(Keys.A, KeyBindings.Default.Get(GameAction.AttackMove));   // immutable
    }

    [Fact]
    public void Conflict_is_found_for_a_key_another_action_uses()
    {
        Assert.Equal(GameAction.Stop, KeyBindings.Default.ConflictOf(GameAction.AttackMove, Keys.S));
        Assert.Null(KeyBindings.Default.ConflictOf(GameAction.AttackMove, Keys.F));
        Assert.Null(KeyBindings.Default.ConflictOf(GameAction.AttackMove, Keys.A));   // its own key is not a conflict
    }

    [Fact]
    public void Play_and_spectator_actions_may_share_a_key_but_both_context_actions_may_not()
    {
        Assert.Null(KeyBindings.Default.ConflictOf(GameAction.SpecDetail, Keys.A));   // Spectator vs Play: a clash only when contexts overlap
        Assert.NotNull(KeyBindings.Default.ConflictOf(GameAction.SpecDetail, Keys.Space));   // Pause is read in both
    }

    [Fact]
    public void Swap_gives_the_other_action_the_old_key()
    {
        KeyBindings? b = KeyBindings.Default.TrySwap(GameAction.AttackMove, Keys.S);
        Assert.NotNull(b);
        Assert.Equal(Keys.S, b!.Get(GameAction.AttackMove));
        Assert.Equal(Keys.A, b.Get(GameAction.Stop));
        Assert.Empty(b.Conflicts());
    }

    [Theory]
    [InlineData(Keys.Escape)]
    [InlineData(Keys.F1)]
    [InlineData(Keys.LeftShift)]
    [InlineData(Keys.RightControl)]
    [InlineData(Keys.Back)]
    [InlineData(Keys.None)]
    public void Reserved_keys_cannot_be_bound(Keys key)
    {
        Assert.False(KeyBindings.IsBindable(key));
        Assert.Null(KeyBindings.Default.TrySwap(GameAction.Stop, key));
    }

    [Fact]
    public void Session_binds_a_free_key()
    {
        var s = new RebindSession();
        s.Begin(GameAction.Stop);
        KeyBindings? b = s.Press(KeyBindings.Default, Keys.F);
        Assert.Equal(Keys.F, b!.Get(GameAction.Stop));
        Assert.Equal(RebindSession.Phase.Idle, s.State);
    }

    [Fact]
    public void Session_shows_a_conflict_and_changes_nothing_until_swap()
    {
        var s = new RebindSession();
        s.Begin(GameAction.AttackMove);
        Assert.Null(s.Press(KeyBindings.Default, Keys.S));
        Assert.Equal(RebindSession.Phase.Conflict, s.State);
        Assert.Equal(GameAction.Stop, s.ConflictWith);
        Assert.Contains("already used", s.Message);
        KeyBindings? b = s.Swap(KeyBindings.Default);
        Assert.Equal(Keys.S, b!.Get(GameAction.AttackMove));
        Assert.Equal(Keys.A, b.Get(GameAction.Stop));
        Assert.Equal(RebindSession.Phase.Idle, s.State);
    }

    [Fact]
    public void Session_cancel_in_conflict_and_escape_while_listening_change_nothing()
    {
        var s = new RebindSession();
        s.Begin(GameAction.AttackMove);
        s.Press(KeyBindings.Default, Keys.S);
        s.Cancel();
        Assert.Equal(RebindSession.Phase.Idle, s.State);
        Assert.Null(s.Swap(KeyBindings.Default));

        s.Begin(GameAction.AttackMove);
        Assert.Null(s.Press(KeyBindings.Default, Keys.Escape));
        Assert.Equal(RebindSession.Phase.Idle, s.State);
    }

    [Fact]
    public void Session_refuses_a_reserved_key_and_keeps_listening()
    {
        var s = new RebindSession();
        s.Begin(GameAction.Stop);
        Assert.Null(s.Press(KeyBindings.Default, Keys.LeftShift));
        Assert.Equal(RebindSession.Phase.Listening, s.State);
        Assert.NotEmpty(s.Message);
    }

    [Fact]
    public void Reset_restores_every_default()
    {
        Settings changed = Settings.Default.WithBindings(KeyBindings.Default.With(GameAction.Stop, Keys.F).With(GameAction.Guard, Keys.H));
        Assert.False(changed.Bindings.IsAllDefault);
        Settings reset = ControlsScreen.ResetKeys(changed);
        Assert.True(reset.Bindings.IsAllDefault);
        Assert.Equal(Settings.Default, reset);
    }

    [Fact]
    public void Bindings_survive_a_save_and_load()
    {
        Settings s = Settings.Default.WithBindings(KeyBindings.Default.TrySwap(GameAction.AttackMove, Keys.S)!.With(GameAction.Pack, Keys.F12));
        string toml = s.ToToml();
        Assert.Contains("[keys]", toml);
        Settings back = Settings.Parse(toml);
        Assert.Equal(Keys.S, back.Bindings.Get(GameAction.AttackMove));
        Assert.Equal(Keys.A, back.Bindings.Get(GameAction.Stop));
        Assert.Equal(Keys.F12, back.Bindings.Get(GameAction.Pack));
        Assert.Equal(s, back);
    }

    [Fact]
    public void Default_settings_write_no_keys_table_and_old_files_load_with_defaults()
    {
        Assert.DoesNotContain("[keys]", Settings.Default.ToToml());
        Settings old = Settings.Parse("version = 2\nfullscreen = true\n");
        Assert.True(old.Bindings.IsAllDefault);
        Assert.True(old.Fullscreen);
    }

    [Fact]
    public void Unknown_ids_bad_key_names_and_reserved_keys_in_the_file_are_ignored()
    {
        Settings s = Settings.Parse("[keys]\nstop = \"F\"\nnot_an_action = \"G\"\nguard = \"NoSuchKey\"\nscatter = \"Escape\"\npack = 5\n");
        Assert.Equal(Keys.F, s.Bindings.Get(GameAction.Stop));
        Assert.Equal(Keys.G, s.Bindings.Get(GameAction.Guard));
        Assert.Equal(Keys.X, s.Bindings.Get(GameAction.Scatter));
        Assert.Equal(Keys.K, s.Bindings.Get(GameAction.Pack));
    }

    [Fact]
    public void A_file_with_clashing_keys_falls_back_to_defaults_for_the_clash()
    {
        Settings s = Settings.Parse("[keys]\nstop = \"G\"\nattack_move = \"F\"\n");   // Stop now clashes with Guard's default
        Assert.Empty(s.Bindings.Conflicts());
        Assert.Equal(Keys.S, s.Bindings.Get(GameAction.Stop));
        Assert.Equal(Keys.G, s.Bindings.Get(GameAction.Guard));
        Assert.Equal(Keys.F, s.Bindings.Get(GameAction.AttackMove));
    }

    [Fact]
    public void Key_names_are_readable()
    {
        Assert.Equal(".", KeyNames.Of(Keys.OemPeriod));
        Assert.Equal("1", KeyNames.Of(Keys.D1));
        Assert.Equal("Home", KeyNames.Of(Keys.Home));
    }
}
