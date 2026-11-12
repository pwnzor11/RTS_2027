// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Screens;

namespace Rts.Game.Lan;

/// <summary>
/// The Esc menu of a LAN match (M9-07): back to the game, the controls list, or leave the match. It opens over the running match, which
/// never pauses for it (design 20 §4.8); push it inside a <see cref="LanCover"/>. There is no save, load, restart or game speed in LAN.
/// </summary>
public sealed class LanMenuScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the menu.</summary>
    public LanMenuScreen(GameServices services)
    {
        _s = services ?? throw new ArgumentNullException(nameof(services));
        _panel = new MenuPanel(LanText.MenuTitle, new[]
        {
            new MenuItem(() => LanText.MenuResume, () => _s.Screens.Pop()),
            new MenuItem(() => LanText.MenuControls, () => _s.Screens.Push(new LanCover(new ControlsScreen(_s)))),
            new MenuItem(() => LanText.MenuLeave, () => GameFlow.QuitToMenu(_s)),
        });
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;   // the LanCover around it lets the match run

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
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }
}
