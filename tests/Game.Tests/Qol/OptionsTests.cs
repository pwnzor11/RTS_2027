// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Audio;
using Rts.Game.Controls;
using Rts.Game.Screens;
using Rts.Game.Ui.Alerts;

namespace Rts.Game.Tests.Qol;

/// <summary>QOL-12, QOL-13 and QOL-20 (D-313): alert volumes, the first-run offer, reduce flashes and second key bindings.</summary>
public sealed class OptionsTests
{
    // ---- QOL-12 ----

    [Fact]
    public void The_default_mix_is_full_volume_and_changes_nothing()
    {
        Assert.True(AlertMix.Default.IsDefault);
        foreach (AlertCategory c in Enum.GetValues<AlertCategory>()) Assert.Equal(100, AlertMix.Default.Get(c));
        foreach (AudioEventKind k in Enum.GetValues<AudioEventKind>()) Assert.Equal(1f, AlertMix.Default.GainOf(k));
        Assert.Null(Settings.Default.AlertVolumes);
        Assert.True(Settings.Default.Alerts.IsDefault);
    }

    [Fact]
    public void Every_alert_kind_has_a_category_and_the_alert_sounds_map_to_the_matching_one()
    {
        Assert.Equal(AlertCategory.Attack, AlertMix.CategoryOf(AlertKind.HarvesterUnderAttack));
        Assert.Equal(AlertCategory.Losses, AlertMix.CategoryOf(AlertKind.UnitLost));
        Assert.Equal(AlertCategory.Production, AlertMix.CategoryOf(AlertKind.UnitReady));
        Assert.Equal(AlertCategory.Production, AlertMix.CategoryOf(AlertKind.HarvesterIdle));
        Assert.Equal(AlertCategory.Power, AlertMix.CategoryOf(AlertKind.LowPower));
        Assert.Equal(AlertCategory.Superweapon, AlertMix.CategoryOf(AlertKind.SuperweaponReady));
        Assert.Equal(AlertCategory.Attack, AlertMix.CategoryOf(AudioEventKind.UnderAttack));
        Assert.Equal(AlertCategory.Production, AlertMix.CategoryOf(AudioEventKind.UnitReady));
        Assert.Null(AlertMix.CategoryOf(AudioEventKind.FireBullet));
    }

    [Fact]
    public void A_category_volume_scales_its_sound_only_and_zero_mutes_it()
    {
        AlertMix mix = AlertMix.Default.With(AlertCategory.Production, 50).With(AlertCategory.Attack, 0);
        Assert.Equal(0.5f, mix.GainOf(AudioEventKind.UnitReady));
        Assert.Equal(0f, mix.GainOf(AudioEventKind.UnderAttack));
        Assert.True(mix.IsMuted(AlertCategory.Attack));
        Assert.False(mix.IsMuted(AlertCategory.Production));
        Assert.Equal(1f, mix.GainOf(AudioEventKind.FireBullet));   // a shot is not an alert
        Assert.Equal(1f, mix.GainOf(AudioEventKind.LowPower));
    }

    [Fact]
    public void The_settings_row_steps_in_quarters_and_clamps()
    {
        Settings s = Settings.Default;
        s = AlertsSettingsScreen.StepVolume(s, AlertCategory.Production, -1);
        Assert.Equal(75, s.Alerts.Get(AlertCategory.Production));
        for (int i = 0; i < 6; i++) s = AlertsSettingsScreen.StepVolume(s, AlertCategory.Production, -1);
        Assert.Equal(0, s.Alerts.Get(AlertCategory.Production));
        Assert.Equal("Production: muted", AlertsSettingsScreen.RowText(s, AlertCategory.Production));
        s = AlertsSettingsScreen.StepVolume(s, AlertCategory.Production, 1);
        Assert.Equal("Production: 25%", AlertsSettingsScreen.RowText(s, AlertCategory.Production));
        for (int i = 0; i < 6; i++) s = AlertsSettingsScreen.StepVolume(s, AlertCategory.Production, 1);
        Assert.Null(s.AlertVolumes);   // all back at 100: stored as the default so equal settings compare equal
    }

    [Fact]
    public void The_mix_is_saved_in_an_alerts_table_and_survives_a_reload()
    {
        Settings s = Settings.Default.WithAlerts(AlertMix.Default.With(AlertCategory.Power, 0).With(AlertCategory.Losses, 25));
        string toml = s.ToToml();
        Assert.Contains("[alerts]", toml);
        Settings back = Settings.Parse(toml);
        Assert.Equal(0, back.Alerts.Get(AlertCategory.Power));
        Assert.Equal(25, back.Alerts.Get(AlertCategory.Losses));
        Assert.Equal(100, back.Alerts.Get(AlertCategory.Attack));
        Assert.DoesNotContain("[alerts]", Settings.Default.ToToml());
        Assert.Equal(100, Settings.Parse("version = 2\n[alerts]\nattack = \"loud\"\nunknown = 3\nlosses = 900").Alerts.Get(AlertCategory.Attack));
        Assert.Equal(100, Settings.Parse("version = 2\n[alerts]\nlosses = 900").Alerts.Get(AlertCategory.Losses));   // clamped
    }

    // ---- QOL-13 ----

    [Fact]
    public void The_tutorial_is_offered_once_to_a_player_with_no_saves()
    {
        Assert.True(FirstRunOffer.ShouldOffer(Settings.Default, hasSave: false));
        Assert.False(FirstRunOffer.ShouldOffer(Settings.Default, hasSave: true));   // not new
        Settings marked = FirstRunOffer.MarkOffered(Settings.Default);
        Assert.False(FirstRunOffer.ShouldOffer(marked, hasSave: false));
        Assert.True(Settings.Parse(marked.ToToml()).TutorialOffered);   // remembered across runs
        Assert.False(Settings.Parse("version = 2\nfullscreen = true").TutorialOffered);
    }

    // ---- QOL-20 ----

    [Fact]
    public void Reduce_flashes_is_off_by_default_separate_from_screen_shake_and_saved()
    {
        Assert.False(Settings.Default.ReduceFlashes);
        Assert.True(Settings.Default.ScreenShake);
        Settings s = Settings.Default with { ReduceFlashes = true };
        Settings back = Settings.Parse(s.ToToml());
        Assert.True(back.ReduceFlashes);
        Assert.True(back.ScreenShake);
        Assert.False(Settings.Parse((Settings.Default with { ScreenShake = false }).ToToml()).ReduceFlashes);
    }

    [Fact]
    public void A_second_key_is_added_cleared_and_never_clashes()
    {
        KeyBindings b = KeyBindings.Default;
        Assert.Equal(Keys.None, b.GetAlt(GameAction.AttackMove));
        KeyBindings two = b.WithAlt(GameAction.AttackMove, Keys.Insert);
        Assert.Equal(Keys.A, two.Get(GameAction.AttackMove));
        Assert.Equal(Keys.Insert, two.GetAlt(GameAction.AttackMove));
        Assert.True(two.Uses(GameAction.AttackMove, Keys.A) && two.Uses(GameAction.AttackMove, Keys.Insert));
        Assert.False(two.IsAllDefault);
        Assert.Equal(GameAction.AttackMove, two.ConflictOf(GameAction.Stop, Keys.Insert));   // the second key counts as taken
        Assert.Equal(b, two.WithAlt(GameAction.AttackMove, Keys.None));   // clearing gives the defaults back
        Assert.Equal(two, two.WithAlt(GameAction.AttackMove, Keys.A));   // the action's own first key is no second key
        Assert.Empty(two.Conflicts());
    }

    [Fact]
    public void Second_keys_are_saved_in_keys_alt_and_a_clashing_file_falls_back_to_the_defaults()
    {
        Settings s = Settings.Default.WithBindings(KeyBindings.Default.WithAlt(GameAction.Stop, Keys.Insert));
        string toml = s.ToToml();
        Assert.Contains("[keys_alt]", toml);
        Assert.Equal(Keys.Insert, Settings.Parse(toml).Bindings.GetAlt(GameAction.Stop));
        Assert.DoesNotContain("[keys_alt]", Settings.Default.ToToml());

        // Insert as the second key of two actions: both are dropped, no clash survives
        Settings clash = Settings.Parse("version = 2\n[keys_alt]\nstop = \"Insert\"\nattack_move = \"Insert\"");
        Assert.Empty(clash.Bindings.Conflicts());
        Assert.Equal(Keys.None, clash.Bindings.GetAlt(GameAction.Stop));
        // a second key equal to the first is ignored
        Assert.Null(Settings.Parse("version = 2\n[keys_alt]\nstop = \"S\"").KeyMap);
    }

    [Fact]
    public void The_rebind_flow_sets_clears_and_refuses_a_second_key()
    {
        var session = new RebindSession();
        session.Begin(GameAction.Stop, second: true);
        KeyBindings? set = session.Press(KeyBindings.Default, Keys.Insert);
        Assert.Equal(Keys.Insert, set!.GetAlt(GameAction.Stop));
        Assert.Equal(RebindSession.Phase.Idle, session.State);

        session.Begin(GameAction.Stop, second: true);
        Assert.Null(session.Press(set, Keys.A));   // Attack-move has it: no swap for a second key, the flow stays open
        Assert.Equal(RebindSession.Phase.Listening, session.State);
        Assert.Contains("already used", session.Message);
        Assert.Null(session.Press(set, Keys.S));   // its own first key
        KeyBindings? cleared = session.Press(set, Keys.Delete);
        Assert.Equal(Keys.None, cleared!.GetAlt(GameAction.Stop));
        Assert.Equal(RebindSession.Phase.Idle, session.State);
    }

    [Fact]
    public void The_controls_label_shows_both_keys()
    {
        var row = new ControlRow("Orders", "Stop", GameAction.Stop);
        Assert.Equal("S", row.LabelFor(KeyBindings.Default));
        Assert.Equal("S / Insert", row.LabelFor(KeyBindings.Default.WithAlt(GameAction.Stop, Keys.Insert)));
    }
}
