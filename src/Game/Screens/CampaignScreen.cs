// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;

namespace Rts.Game.Screens;

/// <summary>The campaign menu (D-061 pilot): the Authority missions that exist so far, and Back.</summary>
public sealed class CampaignScreen : IScreen
{
    /// <summary>Folder name of the pilot mission under data/missions.</summary>
    public const string PilotMissionId = "auth_01";

    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the menu.</summary>
    public CampaignScreen(GameServices services)
    {
        _s = services;
        _panel = new MenuPanel("Campaign", new[]
        {
            new MenuItem(() => "Mission 1: Survey", () => _s.Screens.Push(
                new BriefingScreen(_s, PilotMissionId, () => GameScreen.CreateMission(_s, PilotMissionId)))),
            new MenuItem(() => "Back", () => _s.Screens.Pop()),
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
            _s.Screens.Pop();
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
