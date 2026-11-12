// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Campaign;
using Rts.Game.Input;
using Rts.Game.Ui.Fonts;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>The missions of one faction's campaign with their state; Enter on a playable one opens its briefing.</summary>
public sealed class MissionSelectScreen : IScreen
{
    private readonly GameServices _s;
    private readonly CampaignService _svc;
    private readonly string _faction;
    private MenuPanel _panel;
    private IReadOnlyList<MissionSelectRow> _rows = Array.Empty<MissionSelectRow>();
    private Point _mouse;

    /// <summary>Creates the screen.</summary>
    public MissionSelectScreen(GameServices services, CampaignService campaign, string faction)
    {
        _s = services;
        _svc = campaign;
        _faction = faction;
        _panel = Build();
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <summary>Re-reads the progress (after a win) and keeps the highlighted row.</summary>
    public void Refresh()
    {
        int selected = _panel.Selected;
        _panel = Build();
        _panel.Selected = Math.Min(selected, _rows.Count);
    }

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
        if (input.ConsumePressed(Keys.Escape)) { _s.Screens.Pop(); return; }
        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
        string hint = _panel.Selected < _rows.Count ? _rows[_panel.Selected].Hint : "";
        if (hint.Length > 0)
        {
            var scale = _s.UiScale;
            BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(Math.Max(8, (_s.Viewport.Width - BitmapFont.Measure(hint, scale)) / 2), _s.Viewport.Height - 40 * scale), new Color(0xB8, 0xE0, 0xA0), scale);
        }
    }

    private MenuPanel Build()
    {
        string data = CampaignFlow.DataDir(_s);
        _rows = MissionSelectModel.Build(_svc, data, _faction);
        var items = new List<MenuItem>();
        foreach (MissionSelectRow row in _rows)
        {
            MissionSelectRow r = row;
            items.Add(new MenuItem(() => r.Label, r.CanStart ? () => _s.Screens.Push(OpenBriefing(r.Id)) : null));
        }

        items.Add(new MenuItem(() => "Back", () => _s.Screens.Pop()));
        return new MenuPanel(FactionPickModel.NameOf(_faction) + " campaign", items);
    }

    private BriefingScreen OpenBriefing(string missionId) =>
        new(_s, missionId, tier => LoadingCampaign.ForMission(_s, _faction, missionId, () => CampaignFlow.CreateMission(_s, _svc, _faction, missionId, tier)), _faction, _svc.Progress(_faction).Difficulty.GetValueOrDefault(missionId, 3));
}
