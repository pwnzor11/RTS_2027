// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Ui.Fonts;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>The debrief after a won campaign mission: time, objectives, new tech and Archive entries, then Continue.</summary>
public sealed class DebriefScreen : IScreen
{
    private readonly GameServices _s;
    private readonly DebriefModel _model;
    private readonly Action _continue;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="model">What to show.</param>
    /// <param name="onContinue">Runs on Continue or Esc (leaves the mission).</param>
    public DebriefScreen(GameServices services, DebriefModel model, Action onContinue)
    {
        _s = services;
        _model = model;
        _continue = onContinue;
        _panel = new MenuPanel("Victory: " + model.Title, new[] { new MenuItem(() => model.FinalMission ? "Continue to the ending" : "Continue", _continue) });
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
        var lines = new List<string>(_model.Lines());
        int width = lines.Max(l => BitmapFont.Measure(l, scale)) + 16;
        Hud.Panel(sb, _s.Pixel, new Point(Math.Max(0, (_s.Viewport.Width - width) / 2), 16), lines, scale);
    }
}
