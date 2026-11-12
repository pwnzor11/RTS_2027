// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Ui.Sidebar;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.S19;

/// <summary>M13-15 item 3 (D-205): the sidebar cameo grid has rebindable hotkeys by grid position.</summary>
public sealed class CameoHotkeyTests : IDisposable
{
    private const int W = 1280, H = 720;
    private readonly string _rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    private readonly string _replay = Path.Combine(Path.GetTempPath(), "rts_s19hot_" + Guid.NewGuid().ToString("N") + ".rpl");
    private readonly SimSnapshotSource _source;
    private readonly SidebarPanel _panel;
    private readonly Camera _camera = new();
    private KeyboardState _prevKeys;
    private MouseState _mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);

    public CameoHotkeyTests()
    {
        _source = new SimSnapshotSource(SkirmishGame.Create(_rulesDir, 0, 1), _rulesDir, _replay);
        _panel = new SidebarPanel(_source, 0, new Selection(), null, Color.Green);
        Frame();
    }

    public void Dispose()
    {
        _panel.Dispose();
        _source.SaveReplay();
        foreach (string f in new[] { _replay, _replay + ".prev", _replay + ".map.toml" })
            if (File.Exists(f)) File.Delete(f);
    }

    private void Frame(params Keys[] down)
    {
        var kb = new KeyboardState(down);
        _panel.Update(new InputFrame(kb, _prevKeys, _mouse, _mouse), _camera, W, H);
        _prevKeys = kb;
    }

    private void Tap(Keys key)
    {
        Frame(key);
        Frame();
    }

    [Fact]
    public void The_twelve_slots_map_to_three_columns_by_four_rows()
    {
        Assert.Equal(12, CameoHotkeys.Actions.Count);
        Assert.Equal(0, CameoHotkeys.IndexOf(0, 0));
        Assert.Equal(5, CameoHotkeys.IndexOf(1, 2));
        Assert.Equal(11, CameoHotkeys.IndexOf(3, 2));
        Assert.Equal(-1, CameoHotkeys.IndexOf(4, 0));
        Assert.Equal(-1, CameoHotkeys.IndexOf(0, 3));
    }

    [Fact]
    public void Default_keys_are_the_number_pad_laid_out_like_the_grid_and_clash_with_nothing()
    {
        Assert.Empty(KeyBindings.Default.Conflicts());
        Keys[] expected = { Keys.NumPad7, Keys.NumPad8, Keys.NumPad9, Keys.NumPad4, Keys.NumPad5, Keys.NumPad6, Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad0, Keys.Decimal, Keys.Add };
        Assert.Equal(expected, CameoHotkeys.Actions.Select(a => KeyBindings.Default.Get(a)));
    }

    [Fact]
    public void A_cameo_key_can_be_rebound_to_a_letter_that_is_free()
    {
        KeyBindings rebound = KeyBindings.Default.With(GameAction.CameoSlot1, Keys.Z);
        Assert.Equal(Keys.Z, rebound.Get(GameAction.CameoSlot1));
        Assert.Null(rebound.ConflictOf(GameAction.CameoSlot1, Keys.Z));
        Assert.Equal(GameAction.TabStructures, KeyBindings.Default.ConflictOf(GameAction.CameoSlot1, Keys.Q));   // a letter in use is refused (or swapped)
    }

    [Fact]
    public void Entry_lookup_skips_tier_headings_and_empty_cells()
    {
        BuildOption Opt(int type) => new(ProductionCategory.Infantry, type, true, QueueBlockReason.None, 10);
        TierEntry E(int type) => new(Opt(type), 1, false, "");
        var rows = new List<TierRow>
        {
            new(true, 1, false, "", Array.Empty<TierEntry>()),
            new(false, 1, false, "", new[] { E(0), E(1), E(2) }),
            new(true, 2, false, "", Array.Empty<TierEntry>()),
            new(false, 2, false, "", new[] { E(3) }),
        };
        Assert.Equal(1, CameoHotkeys.EntryAt(rows, 1)!.Value.Option.TypeIndex);
        Assert.Equal(3, CameoHotkeys.EntryAt(rows, 3)!.Value.Option.TypeIndex);   // row 2 column 1: the heading did not count
        Assert.Null(CameoHotkeys.EntryAt(rows, 4));
        Assert.Null(CameoHotkeys.EntryAt(rows, 6));
        Assert.Null(CameoHotkeys.EntryAt(rows, 12));
    }

    [Fact]
    public void The_first_slot_key_queues_the_first_cameo_of_the_active_tab_like_a_click()
    {
        BuildOption first = _panel.VisibleOptions[0];
        int before = _source.Host.Pending.Count;
        Tap(Keys.NumPad7);
        Command c = Assert.Single(_source.Host.Pending.Skip(before));
        Assert.Equal(CommandKind.Build, c.Kind);
        Assert.Equal(first.TypeIndex, c.Arg2);
    }

    [Fact]
    public void A_key_for_an_empty_position_does_nothing()
    {
        int before = _source.Host.Pending.Count;
        Tap(Keys.Add);   // row 4 column 3
        Assert.Equal(before, _source.Host.Pending.Count);
        Assert.False(_panel.ActivateCameoSlot(11, shift: false));
    }
}
