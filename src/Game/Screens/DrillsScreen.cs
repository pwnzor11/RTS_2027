// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Drills;
using Rts.Game.Input;
using Rts.Game.Profile;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>S47 U-12: the Drills list. One row per drill with the best medal and best time; the selected drill's medal limits show underneath.</summary>
public sealed class DrillsScreen : IScreen
{
    private readonly GameServices _s;
    private readonly string _profilePath;
    private IReadOnlyList<DrillEntry> _entries;
    private MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the screen over the game's profile file.</summary>
    public DrillsScreen(GameServices services)
        : this(services, ProfileRecorder.PathFor(services))
    {
    }

    /// <summary>Creates the screen over a given profile file (tests).</summary>
    public DrillsScreen(GameServices services, string profilePath)
    {
        _s = services;
        _profilePath = profilePath;
        _entries = Load();
        _panel = BuildPanel();
    }

    /// <summary>The rows the screen shows.</summary>
    public IReadOnlyList<DrillEntry> Entries => _entries;

    /// <summary>Re-reads the profile (after a drill was won), keeping the selected row.</summary>
    public void Refresh()
    {
        int selected = _panel.Selected;
        _entries = Load();
        _panel = BuildPanel();
        _panel.Selected = Math.Min(selected, _entries.Count);
    }

    /// <summary>The medal limits line for a menu row index; empty for Back.</summary>
    public string HintAt(int panelIndex) => panelIndex >= 0 && panelIndex < _entries.Count ? DrillText.Thresholds(_entries[panelIndex].Def) : "";

    private IReadOnlyList<DrillEntry> Load() => DrillCatalog.Load(CampaignFlow.DataDir(_s), PlayerProfile.Load(_profilePath));

    private MenuPanel BuildPanel()
    {
        var items = new List<MenuItem>();
        foreach (DrillEntry entry in _entries)
        {
            DrillEntry e = entry;
            items.Add(new MenuItem(() => e.Label, () => _s.Screens.Push(DrillFlow.Briefing(_s, e.Id))));
        }

        items.Add(new MenuItem(() => "Back", () => _s.Screens.Pop()));
        return new MenuPanel("Drills", items);
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
        string hint = HintAt(_panel.Selected);
        if (hint.Length > 0)
        {
            var scale = _s.UiScale;
            BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(Math.Max(8, (_s.Viewport.Width - BitmapFont.Measure(hint, scale)) / 2), _s.Viewport.Height - 40 * scale), new Color(0xE0, 0xB0, 0x90), scale);
        }
    }
}
