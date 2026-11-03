// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;

namespace Rts.Game.Screens;

/// <summary>
/// Esc in game: Resume, Restart, Quit to menu. Stops the screens below from updating, so the simulation is paused
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
    public PauseScreen(GameServices services, Func<IScreen> restart)
    {
        _s = services;
        _panel = new MenuPanel("Paused", new[]
        {
            new MenuItem(() => "Resume", () => _s.Screens.Pop()),
            new MenuItem(() => "Restart", () => GameFlow.Restart(_s, restart)),
            new MenuItem(() => "Quit to menu", () => GameFlow.QuitToMenu(_s)),
        });
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
