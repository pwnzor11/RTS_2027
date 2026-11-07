// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Saves;

namespace Rts.Game.Screens;

/// <summary>
/// Esc in game: Resume, Save, Settings, Archive, Restart, Quit to menu. Stops the screens below from updating, so the simulation is paused
/// (single player); they are still drawn, dimmed.
/// </summary>
public sealed class PauseScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the pause menu.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="restart">Builds a fresh game screen for Restart.</param>
    /// <param name="save">What Save game writes to, or null when this game cannot be saved (the sandbox).</param>
    public PauseScreen(GameServices services, Func<IScreen> restart, GameSaveTarget? save = null)
    {
        _s = services;
        var items = new List<MenuItem> { new(() => "Resume", () => Choose(() => _s.Screens.Pop())) };
        if (save is not null)
        {
            items.Add(new MenuItem(() => "Quicksave", () => Choose(() => Saved(save.Save(SaveSlots.Quick), SaveSlots.Quick))));
            items.Add(new MenuItem(() => "Save game", () => Choose(() => _s.Screens.Push(SaveListScreen(save)))));
        }
        items.Add(new MenuItem(() => "Settings", () => Choose(() => _s.Screens.Push(new SettingsScreen(_s)))));
        items.Add(new MenuItem(() => "Archive", () => Choose(() => _s.Screens.Push(new ArchiveScreen(_s)))));
        items.Add(new MenuItem(() => "Restart", () => Choose(() => GameFlow.Restart(_s, restart))));
        items.Add(new MenuItem(() => "Quit to menu", () => Choose(() => GameFlow.QuitToMenu(_s))));
        _panel = new MenuPanel("Paused", items);
    }

    // The slots to save into: a new numbered one first, then the quicksave slot, then every existing save of this kind (overwrite).
    private IScreen SaveListScreen(GameSaveTarget save)
    {
        var rows = new List<ListRow>();
        string next = save.NextName();
        rows.Add(new ListRow("New save: " + next, "Writes a new save slot.", () => SaveAndClose(save, next)));
        rows.Add(new ListRow("Quicksave slot", "Overwrites the quicksave.", () => SaveAndClose(save, SaveSlots.Quick)));
        foreach (SaveEntry e in save.Existing())
        {
            if (e.Name == SaveSlots.Quick) continue;
            string name = e.Name;
            rows.Add(new ListRow("Overwrite: " + name, e.Summary, () => SaveAndClose(save, name)));
        }
        return new ListScreen(_s, "Save game", rows, "", "Choose a slot.");
    }

    private void SaveAndClose(GameSaveTarget save, string slot)
    {
        string? error = save.Save(slot);
        _s.Screens.Pop();   // the slot list
        Saved(error, slot);
    }

    private void Saved(string? error, string slot) =>
        _s.Screens.Push(new NoticeScreen(_s, error is null ? "Saved" : "Not saved", error ?? $"Game saved to \"{slot}\"."));

    private void Choose(Action action)
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Select);
        action();
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => false;

    /// <inheritdoc />
    public void Enter()
    {
    }

    /// <inheritdoc />
    public void Exit()
    {
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape))
        {
            MenuSounds.Play(_s.Audio, MenuSounds.Back);
            _s.Screens.Pop();
            return;
        }

        _panel.Update(input, _s.Viewport, _s.UiScale);
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }
}
