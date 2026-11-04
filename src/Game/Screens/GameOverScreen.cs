// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>
/// The game-over menu (Restart / Quit to menu), pushed by <see cref="GameScreen"/> when its
/// <see cref="GameScreen.IsGameOver"/> check turns true. Its title says "Victory" or "Defeat" (see <see cref="Headline"/>).
/// </summary>
public sealed class GameOverScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private readonly IReadOnlyList<string> _statsLines;
    private Point _mouse;

    /// <summary>Creates the screen. <paramref name="restart"/> builds a fresh game screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="restart">Builds a fresh game screen for Restart.</param>
    /// <param name="headline">The title (see <see cref="Headline"/>); "Game over" when null.</param>
    /// <param name="statsLines">M4-08: the final stats table for all players (header first), drawn above the menu; null for none.</param>
    /// <param name="spectator">True after an AI-vs-AI game: the menu offers Save replay, Watch again (same seed) and Menu.</param>
    /// <param name="saveReplay">Called by "Save replay" (spectator menu); null hides nothing, the item then does nothing.</param>
    public GameOverScreen(GameServices services, Func<IScreen> restart, string? headline = null,
        IReadOnlyList<string>? statsLines = null, bool spectator = false, Action? saveReplay = null)
    {
        _s = services;
        Title = headline ?? "Game over";
        _statsLines = statsLines ?? Array.Empty<string>();
        _panel = new MenuPanel(Title, spectator
            ? new[]
            {
                new MenuItem(() => "Save replay", () => saveReplay?.Invoke()),
                new MenuItem(() => "Watch again", () => GameFlow.Restart(_s, restart)),
                new MenuItem(() => "Menu", () => GameFlow.QuitToMenu(_s)),
            }
            : new[]
            {
                new MenuItem(() => "Restart", () => GameFlow.Restart(_s, restart)),
                new MenuItem(() => "Quit to menu", () => GameFlow.QuitToMenu(_s)),
            });
    }

    /// <summary>The title shown on the menu.</summary>
    public string Title { get; }

    /// <summary>
    /// "Victory" when the local player's team won, otherwise "Defeat" (the local player lost, or everyone was defeated at once).
    /// </summary>
    /// <param name="localDefeated">True if the local player has been defeated.</param>
    /// <param name="winningTeam">The winning team, or -1 for none.</param>
    /// <param name="localTeam">The local player's team.</param>
    public static string Headline(bool localDefeated, int winningTeam, int localTeam) =>
        !localDefeated && winningTeam >= 0 && winningTeam == localTeam ? "Victory" : "Defeat";

    /// <summary>The title after an AI-vs-AI game: "Team N wins" (N from 1), or "Draw" when no team won.</summary>
    /// <param name="winningTeam">The winning team id, or -1 for none.</param>
    public static string SpectatorHeadline(int winningTeam) => winningTeam >= 0 ? $"Team {winningTeam + 1} wins" : "Draw";

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
        if (_statsLines.Count > 0)
        {
            int scale = _s.UiScale;
            int width = _statsLines.Max(line => BitmapFont.Measure(line, scale)) + 16;
            Hud.Panel(sb, _s.Pixel, new Point(Math.Max(0, (_s.Viewport.Width - width) / 2), 16), _statsLines, scale);
        }
    }
}
