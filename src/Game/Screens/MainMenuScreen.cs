// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Update;

namespace Rts.Game.Screens;

/// <summary>
/// The lobby you land in at start-up: an animated illustrated background with the menu down the left (Continue, Campaign,
/// Skirmish, LAN Game, Map Editor, Replays, Archive, How to Play, Settings, Quit). Up/Down/Enter or the mouse. Esc does nothing (Quit is the way out).
/// F9 opens the battle sandbox (debug; it is no longer a menu entry, and <c>--sandbox</c> still starts straight in it).
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private const float RefreshSeconds = 2f;

    private readonly GameServices _s;
    private readonly SaveCatalog _saves;
    private readonly LobbyMenuController _lobby;
    private readonly MenuPanel _plain;   // used only when there is no animated background (headless runs, or it failed to load)
    private Point _mouse;
    private readonly List<DrawOp> _ops = new(512);
    private float _sinceRefresh;
    private float _seconds;
    private bool _hadSave;

    /// <summary>Creates the menu. <paramref name="saves"/> overrides where saves are looked for (tests); null = the default folder.</summary>
    public MainMenuScreen(GameServices services, SaveCatalog? saves = null)
    {
        _s = services;
        _saves = saves ?? new SaveCatalog(SaveCatalog.DefaultRoot);
        _hadSave = _saves.Newest() is not null;
        _lobby = new LobbyMenuController(Entries(_hadSave));
        _plain = new MenuPanel("RTS_2027", _lobby.Entries.Select(e => new MenuItem(
            () => e.Enabled || e.Tooltip is null ? e.Label : $"{e.Label}   ({e.Tooltip})",
            e.Enabled ? () => Activate(e.Action) : null)).ToArray());
    }

    private static IReadOnlyList<MainMenuEntry> Entries(bool hasSave) => MainMenuModel.Build(hasSave);

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

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
        float dt = (float)t.ElapsedGameTime.TotalSeconds;
        _seconds += dt;
        _sinceRefresh += dt;
        if (_sinceRefresh >= RefreshSeconds)
        {
            _sinceRefresh = 0f;
            bool has = _saves.Newest() is not null;
            if (has != _hadSave)
            {
                _hadSave = has;
                _lobby.SetEntries(Entries(has));
            }
        }

        // S19 (D-205): Esc on the main menu does nothing; the Quit row is the way out.
        input.ConsumePressed(Keys.Escape);
        // ASSUMED (M3-16): F9 is the debug key that opens the sandbox now that the menu entry is gone.
        if (input.ConsumePressed(Keys.F9))
        {
            _s.Screens.Push(GameScreen.CreateSandbox(_s, _s.LegacyEmptySandbox, null));
            return;
        }

        if (_s.Background is null)
        {
            _mouse = input.MousePosition;
            _plain.Update(input, _s.Viewport, _s.UiScale);
            return;
        }

        LobbyResult result = _lobby.Update(input, _s.Viewport.Width, _s.Viewport.Height, _s.UiScale);
        if (result.SoundId is not null) MenuSounds.Play(_s.Audio, result.SoundId);
        if (result.Action is { } action) Activate(action);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Viewport vp = _s.Viewport;
        if (_s.Background is not { } background)
        {
            Backdrop.FillOpaque(sb, _s);
            _plain.Draw(sb, _s.Pixel, vp, _s.UiScale, _mouse);
            if (_s.Updates.Found is { } plainUpdate)
                Ui.BitmapFont.Draw(sb, _s.Pixel, UpdateCheck.NoticeText(plainUpdate), new Vector2(8, 8), new Color(0xE8, 0xA8, 0x28), _s.UiScale);
            return;
        }

        background.Draw(sb, vp);
        _ops.Clear();
        LobbyMenuView.Build(_ops, _lobby.Layout, _lobby.Entries, _lobby.Selected, _seconds, background.Caption, BuildInfo.Version, vp.Width, vp.Height);
        if (_s.Updates.Found is { } update)
        {
            // M10-23: a small line above the footer, left side.
            Ui.UiUnit unit = _lobby.Layout.Scale;
            int line = Ui.BitmapFont.LineHeight(unit, Ui.Fonts.FontWeight.Regular);
            _ops.Add(DrawOp.Text(UpdateCheck.NoticeText(update), _lobby.Layout.TitleX + 5 * unit, LobbyMenuView.FooterTop(unit, vp.Height) - line - 2 * unit,
                unit, new Rgba(0xE8, 0xA8, 0x28)));
        }
        background.DrawOps(sb, _ops, vp);
    }

    private void Activate(MenuAction action)
    {
        switch (action)
        {
            case MenuAction.Continue: Continue(); break;
            case MenuAction.Campaign: _s.Screens.Push(new CampaignScreen(_s)); break;
            case MenuAction.Skirmish: _s.Screens.Push(new ModeScreen(_s, SaveKind.Skirmish)); break;
            case MenuAction.LanGame: _s.Screens.Push(new LanBrowserScreen(_s)); break;
            case MenuAction.MapEditor: _s.Screens.Push(new Editor.MapEditorScreen(_s, path => _s.Screens.Push(new SkirmishSetupScreen(_s, new MapChoice(path, null))))); break;
            case MenuAction.Replays: _s.Screens.Push(LoadGameScreen.CreateReplays(_s)); break;
            case MenuAction.Archive: _s.Screens.Push(new ArchiveScreen(_s)); break;
            case MenuAction.Profile: _s.Screens.Push(new Profile.ProfileScreen(_s)); break;
            case MenuAction.HowToPlay: _s.Screens.Push(new HowToPlayScreen(_s)); break;
            case MenuAction.Settings: _s.Screens.Push(new SettingsScreen(_s)); break;
            case MenuAction.Quit: _s.Quit(); break;
        }
    }

    // Resume the newest save; a save that cannot be loaded says why in a notice.
    private void Continue()
    {
        SaveEntry? save = _saves.Newest();
        if (save is null)
        {
            _s.Screens.Push(new NoticeScreen(_s, "Continue", MainMenuModel.NoSaves));
            return;
        }

        LoadGameScreen.Start(_s, save);
    }
}
