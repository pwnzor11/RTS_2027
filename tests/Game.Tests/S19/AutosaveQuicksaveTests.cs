// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Menu;
using Rts.Game.Saves;
using Rts.Game.Tests.Saves;
using Rts.Game.Ui;
using Rts.Sim.Core;

namespace Rts.Game.Tests.S19;

/// <summary>D-205 (S18 GS-06): autosave rules, rotating slots, the quicksave keys and the toast.</summary>
public sealed class AutosaveQuicksaveTests
{
    private static SaveEntry Entry(string name, int minutesAgo) =>
        new(SaveKind.Skirmish, "x/" + name + ".rtss", name, new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc).AddMinutes(-minutesAgo), "", true);

    [Fact]
    public void Default_interval_is_five_minutes_and_off_is_zero_ticks()
    {
        Assert.Equal(5, Settings.Default.AutosaveMinutes);
        Assert.Equal(0, Autosave.IntervalTicks(0));
        Assert.Equal(5 * 60 * SimConstants.TicksPerSecond, Autosave.IntervalTicks(5));
    }

    [Fact]
    public void First_autosave_is_due_at_once_then_every_interval()
    {
        int interval = Autosave.IntervalTicks(5);
        Assert.True(Autosave.Due(0, -1, interval));                  // match start
        Assert.False(Autosave.Due(interval - 1, 0, interval));
        Assert.True(Autosave.Due(interval, 0, interval));
        Assert.False(Autosave.Due(0, -1, Autosave.IntervalTicks(0))); // off: never
    }

    [Fact]
    public void Slots_fill_in_order_then_the_oldest_is_overwritten()
    {
        var none = new List<SaveEntry>();
        Assert.Equal("autosave 1", Autosave.NextSlot(none));
        Assert.Equal("autosave 2", Autosave.NextSlot(new[] { Entry("autosave 1", 5) }));
        Assert.Equal("autosave 3", Autosave.NextSlot(new[] { Entry("autosave 1", 10), Entry("autosave 2", 5) }));
        // all three exist: the one written longest ago goes next, whatever its number
        Assert.Equal("autosave 2", Autosave.NextSlot(new[] { Entry("autosave 1", 5), Entry("autosave 2", 15), Entry("autosave 3", 10) }));
        Assert.Equal("autosave 3", Autosave.NextSlot(new[] { Entry("autosave 1", 5), Entry("autosave 2", 10), Entry("autosave 3", 15), Entry("quicksave", 99) }));
    }

    [Fact]
    public void Interval_steps_wrap_and_unknown_values_restart_at_off()
    {
        Assert.Equal(10, Autosave.StepMinutes(5, 1));
        Assert.Equal(3, Autosave.StepMinutes(5, -1));
        Assert.Equal(0, Autosave.StepMinutes(60, 1));
        Assert.Equal(60, Autosave.StepMinutes(0, -1));
        Assert.Equal(0, Autosave.StepMinutes(7, 1));
        Assert.Equal("Off", Autosave.IntervalText(0));
        Assert.Equal("every 5 min", Autosave.IntervalText(5));
    }

    [Fact]
    public void Setting_round_trips_and_clamps()
    {
        Assert.Equal(2, Settings.Parse(Settings.Default.ToToml().Replace("autosave_minutes = 5", "autosave_minutes = 2")).AutosaveMinutes);
        Assert.Equal(0, Settings.Parse("autosave_minutes = 0").AutosaveMinutes);
        Assert.Equal(Settings.MaxAutosaveMinutes, Settings.Parse("autosave_minutes = 9999").AutosaveMinutes);
        Assert.Equal(5, Settings.Parse("version = 2").AutosaveMinutes);   // an older file gets the default
        Assert.Equal(7, Settings.Parse((Settings.Default with { AutosaveMinutes = 7 }).ToToml()).AutosaveMinutes);
    }

    [Fact]
    public void Quick_keys_default_to_F5_and_F9_and_nothing_conflicts()
    {
        Assert.Equal(Keys.F5, GameKeys.InfoOf(GameAction.QuickSave).Default);
        Assert.Equal(Keys.F9, GameKeys.InfoOf(GameAction.QuickLoad).Default);
        Assert.Empty(KeyBindings.Default.Conflicts());
        // the old F5 bookmark and F9 sandbox-spectator defaults moved away
        Assert.DoesNotContain(new[] { GameAction.Bookmark1, GameAction.Bookmark2, GameAction.Bookmark3, GameAction.Bookmark4, GameAction.DebugSpectator },
            a => GameKeys.InfoOf(a).Default is Keys.F5 or Keys.F9);
        Assert.Contains(GameKeys.Rows, r => r.Action == GameAction.QuickSave);
        Assert.Contains(GameKeys.Rows, r => r.Action == GameAction.QuickLoad);
    }

    [Fact]
    public void Quick_keys_can_be_rebound()
    {
        KeyBindings b = KeyBindings.Default.With(GameAction.QuickSave, Keys.F11);
        Assert.Equal(Keys.F11, b.Get(GameAction.QuickSave));
        Assert.Empty(b.Conflicts());
    }

    [Fact]
    public void Quicksave_and_autosave_slots_are_written_and_listed()
    {
        using var t = new SaveTestSupport();
        (Rts.Game.Host.SimHost host, Rts.Game.Sandbox.SkirmishGame game) = t.NewSkirmish();
        var target = new GameSaveTarget(t.Catalog, game.SaveTemplate(Rts.Game.Screens.SkirmishSettings.Default), host, t.RulesDir);

        Assert.Null(target.Save(SaveSlots.Quick));
        for (int i = 0; i < 4; i++)
        {
            string slot = Autosave.NextSlot(target.Existing());
            Assert.Null(target.Save(slot));
            File.SetLastWriteTimeUtc(SaveSlots.PathFor(t.Catalog, SaveKind.Skirmish, slot), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i));
        }

        IReadOnlyList<string> names = target.Existing().Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "autosave 1", "autosave 2", "autosave 3", "quicksave" }, names);   // never more than 3 autosaves
        Assert.All(target.Existing(), e => Assert.True(e.Readable));
    }

    [Fact]
    public void Toast_shows_for_its_time_and_a_new_line_replaces_the_old()
    {
        var toast = new GameToast();
        Assert.Null(toast.Current(0));
        toast.Show("one", 1000);
        Assert.Equal("one", toast.Current(1000 + GameToast.DefaultMs - 1));
        Assert.Null(toast.Current(1000 + GameToast.DefaultMs));
        toast.Show("two", 5000, 100);
        Assert.Equal("two", toast.Current(5050));
        Assert.Null(toast.Current(5100));
    }
}
