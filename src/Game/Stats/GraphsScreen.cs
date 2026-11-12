// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Screens;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Stats;

/// <summary>
/// M10-17: the Graphs tab of the end-of-match screens. One line chart per metric, one line per player in house colours, a legend
/// and the metric tabs. Left / Right (or a click on a tab) change the metric; Esc, Enter or Back closes it.
/// </summary>
public sealed class GraphsScreen : IScreen
{
    private static readonly GraphMetric[] Metrics = Enum.GetValues<GraphMetric>();

    private readonly GameServices _s;
    private readonly MatchTimeline? _timeline;
    private readonly Color[] _colours;
    private readonly string[] _names;
    private readonly List<Rectangle> _tabRects = new();
    private Rectangle _backRect;
    private Point _mouse;
    private int _metric;

    /// <summary>Creates the screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="timeline">The samples; null or empty shows "No data yet".</param>
    /// <param name="colours">House colour of each timeline column.</param>
    /// <param name="names">Legend text of each timeline column.</param>
    public GraphsScreen(GameServices services, MatchTimeline? timeline, IReadOnlyList<Color> colours, IReadOnlyList<string> names)
    {
        _s = services;
        _timeline = timeline;
        _colours = colours.ToArray();
        _names = names.ToArray();
    }

    /// <summary>Creates the screen for a timeline, taking house colours from the current <see cref="Rts.Game.Sprites.PlayerColours"/> assignment and naming players "Player N".</summary>
    public static GraphsScreen For(GameServices services, MatchTimeline? timeline)
    {
        int[] slots = timeline?.Slots.ToArray() ?? Array.Empty<int>();
        return new GraphsScreen(services, timeline, slots.Select(Rts.Game.Sprites.PlayerColours.Base).ToArray(), slots.Select(p => "Player " + (p + 1)).ToArray());
    }

    /// <summary>The metric tab label.</summary>
    public static string LabelOf(GraphMetric metric) => metric switch
    {
        GraphMetric.ArmyValue => "Army value",
        GraphMetric.Income => "Credits earned",
        GraphMetric.Kills => "Kills",
        GraphMetric.Buildings => "Buildings",
        GraphMetric.Upgrades => "Upgrades",
        _ => metric.ToString(),
    };

    /// <summary>The metric shown now.</summary>
    public GraphMetric Metric => Metrics[_metric];

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
        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(Keys.Enter)) { _s.Screens.Pop(); return; }
        if (input.ConsumePressed(Keys.Right) || input.ConsumePressed(Keys.D)) _metric = (_metric + 1) % Metrics.Length;
        if (input.ConsumePressed(Keys.Left) || input.ConsumePressed(Keys.A)) _metric = (_metric + Metrics.Length - 1) % Metrics.Length;
        if (input.LeftPressed)
        {
            if (_backRect.Contains(_mouse)) { input.ConsumeMouse(); _s.Screens.Pop(); return; }
            for (int i = 0; i < _tabRects.Count; i++)
                if (_tabRects[i].Contains(_mouse)) _metric = i;
        }

        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        UiUnit u = _s.UiScale;
        Texture2D px = _s.Pixel;
        const int margin = 16;
        int lh = BitmapFont.LineHeight(u, FontWeight.Regular);
        BitmapFont.Draw(sb, px, "Graphs", new Vector2(margin, margin), Color.White, u, FontWeight.Bold);

        // Tabs.
        _tabRects.Clear();
        int x = margin, y = margin + lh + 8;
        for (int i = 0; i < Metrics.Length; i++)
        {
            string label = LabelOf(Metrics[i]);
            var r = new Rectangle(x, y, BitmapFont.Measure(label, u) + 16, lh + 8);
            _tabRects.Add(r);
            bool on = i == _metric;
            Shapes.Fill(sb, px, r, on ? new Color(70, 90, 120) : r.Contains(_mouse) ? new Color(50, 60, 80) : new Color(30, 36, 48));
            BitmapFont.Draw(sb, px, label, new Vector2(r.X + 8, r.Y + 4), on ? Color.White : new Color(190, 195, 205), u);
            x = r.Right + 6;
        }

        // Back button, bottom left.
        const string back = "Back";
        _backRect = new Rectangle(margin, _s.Viewport.Height - margin - lh - 8, BitmapFont.Measure(back, u) + 24, lh + 8);
        Shapes.Fill(sb, px, _backRect, _backRect.Contains(_mouse) ? new Color(70, 90, 120) : new Color(40, 50, 66));
        BitmapFont.Draw(sb, px, back, new Vector2(_backRect.X + 12, _backRect.Y + 4), Color.White, u);

        int legendW = 0;
        if (_timeline is not null)
            foreach (string n in _names) legendW = Math.Max(legendW, BitmapFont.Measure(n, u));
        legendW += 28;
        var plot = new Rectangle(margin + 4 * lh + 16, y + lh + 8 + 16, _s.Viewport.Width - 2 * margin - legendW - 4 * lh - 24,
            _backRect.Y - (y + lh + 8 + 16) - lh - 24);
        if (plot.Width < 40 || plot.Height < 40) return;

        if (_timeline is null || _timeline.Count == 0)
        {
            BitmapFont.Draw(sb, px, "No data yet", new Vector2(plot.X, plot.Y), new Color(190, 195, 205), u);
            return;
        }

        GraphMetric metric = Metrics[_metric];
        int top = GraphGeometry.AxisTop(_timeline.Max(metric));
        Shapes.Fill(sb, px, plot, new Color(14, 18, 26));
        Shapes.Outline(sb, px, plot, new Color(90, 100, 120));
        for (int g = 0; g <= 4; g++)
        {
            int gy = plot.Bottom - g * plot.Height / 4;
            if (g > 0 && g < 4) Shapes.Fill(sb, px, new Rectangle(plot.X, gy, plot.Width, 1), new Color(40, 46, 60));
            string text = ((long)top * g / 4).ToString();
            BitmapFont.Draw(sb, px, text, new Vector2(plot.X - 6 - BitmapFont.Measure(text, u), gy - lh / 2), new Color(190, 195, 205), u);
        }

        string start = GraphGeometry.ClockText(_timeline.Ticks[0]), end = GraphGeometry.ClockText(_timeline.Ticks[^1]);
        BitmapFont.Draw(sb, px, start, new Vector2(plot.X, plot.Bottom + 6), new Color(190, 195, 205), u);
        BitmapFont.Draw(sb, px, end, new Vector2(plot.Right - BitmapFont.Measure(end, u), plot.Bottom + 6), new Color(190, 195, 205), u);

        for (int c = 0; c < _timeline.Slots.Count; c++)
        {
            Color colour = c < _colours.Length ? _colours[c] : Color.Gray;
            Point[] pts = GraphGeometry.Line(_timeline, c, metric, plot, top);
            for (int i = 1; i < pts.Length; i++)
                Shapes.Line(sb, px, pts[i - 1].ToVector2(), pts[i].ToVector2(), colour, 2);
            if (pts.Length == 1) Shapes.Fill(sb, px, new Rectangle(pts[0].X - 2, pts[0].Y - 2, 4, 4), colour);
            int ly = plot.Y + c * (lh + 6);
            Shapes.Fill(sb, px, new Rectangle(plot.Right + 12, ly + lh / 4, 12, lh / 2), colour);
            BitmapFont.Draw(sb, px, c < _names.Length ? _names[c] : "Player", new Vector2(plot.Right + 30, ly), Color.White, u);
        }
    }
}
