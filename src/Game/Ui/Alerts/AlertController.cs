// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Audio;
using Rts.Game.Render;
using Rts.Game.Ui.Fonts;
using Rts.Sim.Core;

namespace Rts.Game.Ui.Alerts;

/// <summary>
/// The screen's one handle on alerts (M11-10d): feeds each new sim step through the audio event deriver and <see cref="AlertDeriver"/>
/// into an <see cref="AlertLog"/>, draws the stack and the minimap pings, and answers the last-alert key.
/// </summary>
public sealed class AlertController
{
    private static readonly Color Own = new(0x6C, 0xE0, 0x5E);
    private static readonly Color Red = new(0xE8, 0x4A, 0x2E);
    private static readonly Color Amber = new(0xF0, 0xB0, 0x40);
    private static readonly Color Text = new(0xF0, 0xE6, 0xC0);

    private readonly AudioEventDeriver _audio = new();
    private readonly AlertDeriver _deriver;
    private readonly AlertJumpCursor _cursor = new();
    private readonly List<AudioEvent> _events = new();
    private readonly List<GameAlert> _alerts = new();
    private PlayerHud? _previousHud;
    private int _lastTick = -1;

    /// <summary>Creates the controller. <paramref name="isHarvester"/> tells whether a unit is a harvester.</summary>
    public AlertController(Func<UnitView, bool>? isHarvester = null) => _deriver = new AlertDeriver(isHarvester);

    /// <summary>The state (exposed for tests).</summary>
    public AlertLog Log { get; } = new();

    /// <summary>Call once per frame with the snapshot pair of the game and the local player's HUD. Does nothing until the sim has stepped.</summary>
    public void Observe(SimSnapshot previous, SimSnapshot current, PlayerHud hud, byte local)
    {
        if (current.Tick == _lastTick) return;
        bool restart = _lastTick < 0 || current.Tick < _lastTick;
        _lastTick = current.Tick;
        PlayerHud before = _previousHud ?? hud;
        _previousHud = hud;
        if (restart) { Log.Clear(); return; }   // the first frame (or a restart) must not announce everything that already stands
        var frame = new AudioFrame(previous, current, local) { PreviousHud = before, CurrentHud = hud };
        _events.Clear();
        _alerts.Clear();
        _audio.Derive(frame, _events);
        _deriver.Derive(frame, _events, _alerts);
        foreach (GameAlert a in _alerts) Log.Add(a);
    }

    /// <summary>The last-alert key: the world point (zoom-1 pixels) to centre the camera on, or null when there is nothing to go to.</summary>
    public Vector2? JumpTarget(int tick)
    {
        if (_cursor.Press(Log, tick) is not { } at) return null;
        return new Vector2(at.X * Camera.CellPx / (float)SimConstants.LeptonsPerCell, at.Y * Camera.CellPx / (float)SimConstants.LeptonsPerCell);
    }

    /// <summary>Draws the stack at the top right of <paramref name="mapView"/>, clear of the rectangles in <paramref name="avoid"/>.</summary>
    public void DrawStack(SpriteBatch sb, Texture2D pixel, Rectangle mapView, UiUnit scale, int tick, IEnumerable<Rectangle> avoid)
    {
        IReadOnlyList<AlertLine> lines = Log.Lines(tick);
        if (lines.Count == 0) return;
        int pad = 8, stripe = 4, gap = 4;
        int rowH = Hud.HeightOf(1, scale);
        int width = 0;
        foreach (AlertLine l in lines) width = Math.Max(width, BitmapFont.Measure(l.Text, scale));
        width += 2 * pad + stripe;
        Rectangle box = AlertLayout.Place(mapView, new Point(width, lines.Count * rowH + (lines.Count - 1) * gap), 8, 8, avoid);
        for (int i = 0; i < lines.Count; i++)
        {
            AlertLine l = lines[i];
            var row = new Rectangle(box.X, box.Y + i * (rowH + gap), width, rowH);
            Color tone = AlertInfo.Tone(l.Kind) switch { AlertTone.Danger => Red, AlertTone.Warning => Amber, _ => Own };
            Shapes.Fill(sb, pixel, row, Shapes.Alpha(new Color(0, 0, 0), (byte)(190 * l.Alpha / 255)));
            Shapes.Fill(sb, pixel, new Rectangle(row.X, row.Y, stripe, row.Height), Shapes.Alpha(tone, l.Alpha));
            BitmapFont.Draw(sb, pixel, l.Text, new Vector2(row.X + stripe + pad, row.Y + 4), Shapes.Alpha(Text, l.Alpha), scale);
        }
    }

    /// <summary>Draws the alert pings on the minimap: a ring that grows and fades, red for attacks and losses, the player's colour otherwise.</summary>
    public void DrawMinimapPings(SpriteBatch sb, Texture2D pixel, Rectangle area, int mapWidth, int mapHeight, int tick)
    {
        IReadOnlyList<AlertPing> pings = Log.Pings(tick);
        if (pings.Count == 0) return;
        MinimapMath m = MinimapMath.Fit(area, mapWidth, mapHeight);
        if (m.Image.IsEmpty) return;
        foreach (AlertPing p in pings)
        {
            Vector2 at = m.PointFromCell(new Vector2(p.Pos.X / (float)SimConstants.LeptonsPerCell, p.Pos.Y / (float)SimConstants.LeptonsPerCell));
            Color c = p.Danger ? Red : Own;
            float pulse = (p.Age * 3f) % 1f;
            byte fade = (byte)(255 * (1f - p.Age));
            Shapes.Circle(sb, pixel, at, 3f + 8f * pulse, Shapes.Alpha(c, (byte)(fade * (1f - pulse))), 2, 24);
            Shapes.Circle(sb, pixel, at, 3f, Shapes.Alpha(c, fade), 2, 24);
        }
    }
}
