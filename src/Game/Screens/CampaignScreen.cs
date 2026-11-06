// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Campaign;
using Rts.Game.Input;
using Rts.Game.Ui.Fonts;
using Rts.Game.Menu;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>
/// The campaign entry (plan 3.4): the faction pick. Locked factions show why; an open one leads to its mission select. Also offers Load game
/// for saved campaign missions. Replaces the pilot single-mission list; auth_01 is the first Authority mission.
/// </summary>
public sealed class CampaignScreen : IScreen
{
    /// <summary>Folder name of the pilot mission under data/missions (the first Authority mission).</summary>
    public const string PilotMissionId = "auth_01";

    private readonly GameServices _s;
    private readonly CampaignService _svc;
    private readonly IReadOnlyList<FactionPickRow> _rows;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the menu.</summary>
    public CampaignScreen(GameServices services)
    {
        _s = services;
        _svc = CampaignFlow.Service(services);
        _rows = FactionPickModel.Build(_svc);
        var items = new List<MenuItem>();
        foreach (FactionPickRow row in _rows)
        {
            FactionPickRow r = row;
            items.Add(new MenuItem(() => r.Label, r.Open ? () => _s.Screens.Push(new MissionSelectScreen(_s, _svc, r.Faction)) : null));
        }

        items.Add(new MenuItem(() => "Load game", () => _s.Screens.Push(LoadGameScreen.Create(_s, SaveKind.Campaign))));
        items.Add(new MenuItem(() => "Back", () => _s.Screens.Pop()));
        _panel = new MenuPanel("Campaign", items);
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
        string hint = _panel.Selected < _rows.Count ? _rows[_panel.Selected].Reason : "";
        if (hint.Length > 0)
        {
            int scale = _s.UiScale;
            BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(Math.Max(8, (_s.Viewport.Width - BitmapFont.Measure(hint, scale)) / 2), _s.Viewport.Height - 40 * scale), new Color(0xE0, 0xB0, 0x90), scale);
        }
    }
}
