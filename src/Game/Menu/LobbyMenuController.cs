// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Ui;

namespace Rts.Game.Menu;

/// <summary>What the lobby did this frame.</summary>
/// <param name="Action">The entry that was activated, or null.</param>
/// <param name="SoundId">The interface sound to play, or null.</param>
public readonly record struct LobbyResult(MenuAction? Action, string? SoundId);

/// <summary>
/// Keyboard and mouse handling of the main menu: Up/Down (and Tab) move, Home/End jump, Enter or Space activates, hovering selects,
/// a click activates. A disabled entry can be selected (to read its tooltip) but never activates; trying plays the deny sound.
/// </summary>
public sealed class LobbyMenuController
{
    private IReadOnlyList<MainMenuEntry> _entries;

    /// <summary>Creates the controller over an entry list.</summary>
    public LobbyMenuController(IReadOnlyList<MainMenuEntry> entries)
    {
        _entries = entries;
        Selected = MainMenuModel.InitialSelection(entries);
        Layout = LobbyMenuView.Measure(1280, 720, 2, entries);
    }

    /// <summary>The entries.</summary>
    public IReadOnlyList<MainMenuEntry> Entries => _entries;

    /// <summary>Index of the highlighted entry.</summary>
    public int Selected { get; private set; }

    /// <summary>Layout from the last <see cref="Update"/>.</summary>
    public LobbyLayout Layout { get; private set; }

    /// <summary>Swaps the entry list (for example Continue became available), keeping the selection on the same action.</summary>
    public void SetEntries(IReadOnlyList<MainMenuEntry> entries)
    {
        MenuAction was = _entries[Math.Clamp(Selected, 0, _entries.Count - 1)].Action;
        _entries = entries;
        int i = entries.ToList().FindIndex(e => e.Action == was);
        Selected = i >= 0 ? i : MainMenuModel.InitialSelection(entries);
    }

    /// <summary>Handles this frame's input.</summary>
    public LobbyResult Update(InputFrame input, int viewW, int viewH, UiUnit scale)
    {
        Layout = LobbyMenuView.Measure(viewW, viewH, scale, _entries);
        int count = _entries.Count;
        if (count == 0) return default;

        string? sound = null;
        int before = Selected;
        if (input.ConsumePressed(Keys.Down) || input.ConsumePressed(Keys.Tab)) Selected = MainMenuModel.Move(Selected, 1, count);
        if (input.ConsumePressed(Keys.Up)) Selected = MainMenuModel.Move(Selected, -1, count);
        if (input.ConsumePressed(Keys.Home)) Selected = 0;
        if (input.ConsumePressed(Keys.End)) Selected = count - 1;

        Point mouse = input.MousePosition;
        if (input.MouseDelta != Point.Zero)
        {
            for (int i = 0; i < count; i++)
                if (Layout.Rows[i].Contains(mouse)) Selected = i;
        }
        if (Selected != before) sound = MenuSounds.Move;

        bool activate = input.ConsumePressed(Keys.Enter) || input.ConsumePressed(Keys.Space);
        if (input.LeftPressed)
        {
            for (int i = 0; i < count; i++)
            {
                if (!Layout.Rows[i].Contains(mouse)) continue;
                Selected = i;
                activate = true;
                input.ConsumeLeft();
                break;
            }
        }

        if (!activate) return new LobbyResult(null, sound);
        MainMenuEntry entry = _entries[Selected];
        return entry.Enabled ? new LobbyResult(entry.Action, MenuSounds.Select) : new LobbyResult(null, MenuSounds.Deny);
    }
}
