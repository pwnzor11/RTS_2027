// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;

namespace Rts.Game.Screens;

/// <summary>
/// Stub game-over menu (Restart / Quit to menu), pushed by <see cref="GameScreen"/> when its
/// <see cref="GameScreen.IsGameOver"/> check turns true. It does not yet say who won; that arrives with the win rule.
/// </summary>
public sealed class GameOverScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the screen. <paramref name="restart"/> builds a fresh game screen.</summary>
    public GameOverScreen(GameServices services, Func<IScreen> restart)
    {
        _s = services;
        _panel = new MenuPanel("Game over", new[]
        {
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
            GameFlow.QuitToMenu(_s);
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
