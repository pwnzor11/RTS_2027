// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Drills;
using Rts.Game.Input;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>S47 U-12: the end screen of a won drill. It shows the medal earned, the run and the thresholds, then Continue or Retry.</summary>
public sealed class DrillEndScreen : IScreen
{
    private readonly GameServices _s;
    private readonly DrillEndModel _model;
    private readonly Action _continue;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="model">What to show.</param>
    /// <param name="onContinue">Runs on Continue or Esc (back to the Drills list).</param>
    /// <param name="onRetry">Runs on Retry (the drill again).</param>
    public DrillEndScreen(GameServices services, DrillEndModel model, Action onContinue, Action onRetry)
    {
        _s = services;
        _model = model;
        _continue = onContinue;
        _panel = new MenuPanel("Drill complete: " + DrillText.MedalName(model.Medal), new[]
        {
            new MenuItem(() => "Continue", onContinue),
            new MenuItem(() => "Retry", onRetry),
        });
    }

    /// <summary>What the screen shows.</summary>
    public DrillEndModel Model => _model;

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
        if (input.ConsumePressed(Keys.Escape)) { _continue(); return; }
        _panel.Update(input, _s.Viewport, _s.UiScale);
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
        var scale = _s.UiScale;
        var lines = new List<string>(_model.Lines);
        int width = lines.Max(l => BitmapFont.Measure(l, scale)) + 16;
        Hud.Panel(sb, _s.Pixel, new Point(Math.Max(0, (_s.Viewport.Width - width) / 2), 16), lines, scale);
    }
}
