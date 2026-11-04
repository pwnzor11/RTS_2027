// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;

namespace Rts.Game.Screens;

/// <summary>The Campaign or Skirmish entry of the main menu: New game, Load game, Back.</summary>
public sealed class ModeScreen : IScreen
{
    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the screen for one kind of game.</summary>
    public ModeScreen(GameServices services, SaveKind kind)
    {
        _s = services;
        bool campaign = kind == SaveKind.Campaign;
        _panel = new MenuPanel(campaign ? "Campaign" : "Skirmish", new[]
        {
            new MenuItem(() => campaign ? "New Campaign" : "New Skirmish", () =>
                _s.Screens.Push(campaign ? new CampaignScreen(_s) : new SkirmishSetupScreen(_s))),
            new MenuItem(() => campaign ? "Load Campaign" : "Load Skirmish", () => _s.Screens.Push(LoadGameScreen.Create(_s, kind))),
            new MenuItem(() => "Back", Back),
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
            Back();
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

    private void Back()
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Back);
        _s.Screens.Pop();
    }
}
