// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// Text-first mission briefing (D-030): the briefing file typed out like a teletype (Space or a click on the text shows it all),
/// then Start (opens the mission) and Back. Esc goes back.
/// </summary>
public sealed class BriefingScreen : IScreen
{
    private const int WrapChars = 84;
    private const int CharsPerUpdate = 3;
    private static readonly Color TextColour = new(0xB8, 0xE0, 0xA0);
    private static readonly Color HeaderColour = new(0xD8, 0xC8, 0x8A);

    private readonly GameServices _s;
    private readonly string _title;
    private readonly List<string> _lines = new();
    private readonly int _totalChars;
    private readonly MenuPanel _panel;
    private int _shown;
    private Point _mouse;

    /// <summary>Creates the screen from the mission's folder under data/missions.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="missionId">Mission folder name.</param>
    /// <param name="start">Builds the mission's game screen when Start is chosen.</param>
    public BriefingScreen(GameServices services, string missionId, Func<IScreen> start)
    {
        _s = services;
        string dataDir = Path.GetFullPath(Path.Combine(services.RulesDir, ".."));
        string folder = MissionLoader.FolderOf(dataDir, missionId);
        MissionDef def = MissionLoader.Load(folder);
        _title = def.Title;
        foreach (string raw in File.ReadAllLines(Path.Combine(folder, def.BriefingFile)))
        {
            if (raw.Length == 0) { _lines.Add(""); continue; }
            _lines.AddRange(MissionOverlay.Wrap(raw, WrapChars));
        }

        _totalChars = _lines.Sum(l => l.Length + 1);
        _panel = new MenuPanel(def.Title, new[]
        {
            new MenuItem(() => "Start mission", () => _s.Screens.Push(start())),
            new MenuItem(() => "Back", () => _s.Screens.Pop()),
        });
    }

    /// <summary>True once the whole text is shown.</summary>
    public bool Complete => _shown >= _totalChars;

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

        if (!Complete)
        {
            _shown += CharsPerUpdate;
            if (input.ConsumePressed(Keys.Space) || input.LeftPressed) { _shown = _totalChars; input.ConsumeLeft(); }
            return;
        }

        _panel.Update(input, ButtonViewport(), _s.UiScale);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        int scale = _s.UiScale;
        int lineHeight = BitmapFont.LineHeight(scale, FontWeight.Regular) + 4;
        int x = Math.Max(16, (_s.Viewport.Width - BitmapFont.Measure(new string('M', WrapChars), scale)) / 2);
        int y = 24;
        BitmapFont.Draw(sb, _s.Pixel, _title.ToUpperInvariant(), new Vector2(x, y), HeaderColour, scale);
        y += lineHeight * 2;
        int remaining = _shown;
        foreach (string line in _lines)
        {
            if (remaining <= 0) break;
            string visible = line.Length <= remaining ? line : line[..remaining];
            BitmapFont.Draw(sb, _s.Pixel, visible, new Vector2(x, y), TextColour, scale);
            remaining -= line.Length + 1;
            y += lineHeight;
        }

        if (Complete) _panel.Draw(sb, _s.Pixel, ButtonViewport(), scale, _mouse);
    }

    // The menu panel centres itself in the viewport it is given: give it the strip below the text.
    private Viewport ButtonViewport()
    {
        int lineHeight = BitmapFont.LineHeight(_s.UiScale, FontWeight.Regular) + 4;
        int top = Math.Min(_s.Viewport.Height - 120, 24 + lineHeight * (_lines.Count + 3));
        return new Viewport(0, top, _s.Viewport.Width, Math.Max(100, _s.Viewport.Height - top));
    }
}
