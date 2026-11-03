// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;

namespace Rts.Game.Screens;

/// <summary>The main menu (D-062 (2), D-061): Campaign, Skirmish, Sandbox, Quit. Esc quits.</summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the menu.</summary>
    public MainMenuScreen(GameServices services)
    {
        _s = services;
        _panel = new MenuPanel("RTS_2027", new[]
        {
            new MenuItem(() => "Campaign", () => _s.Screens.Push(new CampaignScreen(_s))),
            new MenuItem(() => "Skirmish", () => _s.Screens.Push(new SkirmishSetupScreen(_s))),
            new MenuItem(() => "Sandbox", () => _s.Screens.Push(GameScreen.CreateSandbox(_s, _s.LegacyEmptySandbox, null))),
            new MenuItem(() => "Quit", _s.Quit),
        });
    }

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
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Quit();
            return;
        }

        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }
}
