// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Controls;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.TechTree;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;
using Rts.Game.Ui.Sidebar;

namespace Rts.Game.Screens;

/// <summary>
/// The tech-tree viewer (U-12): a full-window overlay with one faction's tree in columns (tier / depth), each node coloured built, available,
/// locked or locked by the mission. Hovering a node shows its name, cost, build time, what it needs and what it unlocks, and draws its
/// edges. It blocks nothing below: the game keeps running (a LAN game cannot be paused), the overlay only takes the input. The Tech tree
/// key or Esc closes it; the wheel and the arrow keys scroll.
/// </summary>
public sealed class TechTreeScreen : IScreen
{
    private static readonly Color Dim = new(0, 0, 0, 200);
    private static readonly Color Built = new(0x2E, 0x7D, 0x46);
    private static readonly Color Available = new(0x2F, 0x5F, 0xA8);
    private static readonly Color Locked = new(0x3A, 0x3A, 0x3A);
    private static readonly Color MissionLocked = new(0x7A, 0x2A, 0x2A);

    private readonly GameServices _s;
    private readonly TechTreeModel _model;
    private readonly Func<TechNodeStatus[]> _statusOf;
    private TechNodeStatus[] _status;
    private Point _mouse;
    private int _scrollX;
    private int _scrollY;
    private int _hover = -1;
    private Rectangle[] _rects = Array.Empty<Rectangle>();
    private Rectangle _area;
    private Point _content;

    /// <summary>Creates the overlay.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="model">The tree to show.</param>
    /// <param name="statusOf">Reads the current status of every node of <paramref name="model"/> (same order as its nodes); called every frame.</param>
    public TechTreeScreen(GameServices services, TechTreeModel model, Func<TechNodeStatus[]> statusOf)
    {
        _s = services;
        _model = model;
        _statusOf = statusOf;
        _status = statusOf();
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => false;

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
        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(GameAction.TechTree))
        {
            _s.Screens.Pop();
            return;
        }

        _status = _statusOf();
        Layout();
        int step = BitmapFont.LineHeight(_s.UiScale, FontWeight.Regular) * 3;
        if (input.IsDown(Keys.Up)) _scrollY -= step / 4;
        if (input.IsDown(Keys.Down)) _scrollY += step / 4;
        if (input.IsDown(Keys.Left)) _scrollX -= step / 4;
        if (input.IsDown(Keys.Right)) _scrollX += step / 4;
        if (input.ConsumePressed(Keys.PageUp)) _scrollY -= _area.Height * 9 / 10;
        if (input.ConsumePressed(Keys.PageDown)) _scrollY += _area.Height * 9 / 10;
        int wheel = input.WheelDelta / 120;
        if (wheel != 0)
        {
            if (input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift)) _scrollX -= wheel * step;
            else _scrollY -= wheel * step;
        }
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, _content.X - _area.Width));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, _content.Y - _area.Height));
        Layout();

        _hover = -1;
        if (_area.Contains(_mouse))
            for (int i = 0; i < _rects.Length; i++)
                if (_rects[i].Contains(_mouse)) { _hover = i; break; }

        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    // Boxes: one column per depth, rows in node order; positions already include the scroll offset.
    private void Layout()
    {
        UiUnit u = _s.UiScale;
        int m = 8 * u;
        int header = 2 * BitmapFont.LineHeight(u, FontWeight.Bold) + 4 * u;
        _area = new Rectangle(m, m + header + 6 * u, Math.Max(1, _s.Viewport.Width - 2 * m), Math.Max(1, _s.Viewport.Height - header - 6 * u - 2 * m));
        int boxW = 0;
        foreach (TechNode n in _model.Nodes) boxW = Math.Max(boxW, BitmapFont.Measure(CameoNames.Display(n.Id), u, FontWeight.Regular));
        boxW += 8 * u;
        int boxH = BitmapFont.LineHeight(u, FontWeight.Regular) + 4 * u;
        int gapX = 14 * u, gapY = 3 * u;
        var rowOf = new int[Math.Max(1, _model.Columns)];
        if (_rects.Length != _model.Nodes.Count) _rects = new Rectangle[_model.Nodes.Count];
        int maxRows = 0;
        for (int i = 0; i < _model.Nodes.Count; i++)
        {
            int c = _model.Nodes[i].Column;
            int r = rowOf[c]++;
            maxRows = Math.Max(maxRows, rowOf[c]);
            _rects[i] = new Rectangle(_area.X + c * (boxW + gapX) - _scrollX, _area.Y + r * (boxH + gapY) - _scrollY, boxW, boxH);
        }
        _content = new Point(_model.Columns * (boxW + gapX) - gapX, maxRows * (boxH + gapY));
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        UiUnit u = _s.UiScale;
        Texture2D px = _s.Pixel;
        Shapes.Fill(sb, px, new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height), Dim);
        Layout();
        int m = 8 * u;
        string key = KeyNames.Of(GameKeys.Active.Get(GameAction.TechTree));
        Text(sb, UiText.Format(UiText.TechTreeTitle, "faction", TechTreeText.FactionName(_model.Faction).ToUpperInvariant()), m, m, Color.White, FontWeight.Bold);
        int line = BitmapFont.LineHeight(u, FontWeight.Bold);
        Text(sb, UiText.Format(UiText.TechTreeHint, "key", key), m, m + line, new Color(0xB0, 0xB0, 0xB0));
        DrawLegend(sb, m + 2 * line);

        if (_model.Nodes.Count == 0)
        {
            Text(sb, UiText.Get(UiText.TechTreeEmpty), _area.X, _area.Y, Color.White);
            return;
        }

        // Clip to the area by skipping boxes fully outside it (the batch has no scissor).
        var hot = new HashSet<int>();
        if (_hover >= 0)
        {
            TechNode h = _model.Nodes[_hover];
            for (int i = 0; i < _model.Nodes.Count; i++)
                if (h.Needs.Contains(_model.Nodes[i].Id) || h.Unlocks.Contains(_model.Nodes[i].Id)) hot.Add(i);
            foreach (int i in hot)
            {
                bool before = h.Needs.Contains(_model.Nodes[i].Id);
                Rectangle a = before ? _rects[i] : _rects[_hover], b = before ? _rects[_hover] : _rects[i];
                if (!Visible(a) && !Visible(b)) continue;
                Shapes.Line(sb, px, new Vector2(a.Right, a.Center.Y), new Vector2(b.Left, b.Center.Y), before ? new Color(0xE0, 0xC0, 0x50) : new Color(0x60, 0xD0, 0xE0), Math.Max(1, u.Whole));
            }
        }

        for (int i = 0; i < _model.Nodes.Count; i++)
        {
            Rectangle r = _rects[i];
            if (!Visible(r)) continue;
            TechNodeStatus st = _status[i];
            Shapes.Fill(sb, px, r, ColourOf(st));
            Shapes.Outline(sb, px, r, i == _hover ? Color.White : hot.Contains(i) ? new Color(0xE0, 0xC0, 0x50) : new Color(0, 0, 0, 160), Math.Max(1, u.Whole));
            Color text = st is TechNodeStatus.Locked or TechNodeStatus.MissionLocked ? new Color(0xA0, 0xA0, 0xA0) : Color.White;
            Text(sb, CameoNames.Display(_model.Nodes[i].Id), r.X + 4 * u, r.Y + 2 * u, text);
        }

        if (_hover >= 0) DrawTooltip(sb);
    }

    private bool Visible(Rectangle r) => r.Right > _area.Left && r.Left < _area.Right && r.Bottom > _area.Top && r.Top < _area.Bottom;

    private static Color ColourOf(TechNodeStatus s) => s switch
    {
        TechNodeStatus.Built => Built,
        TechNodeStatus.Available => Available,
        TechNodeStatus.MissionLocked => MissionLocked,
        _ => Locked,
    };

    private void DrawLegend(SpriteBatch sb, int y)
    {
        UiUnit u = _s.UiScale;
        int x = 8 * u;
        foreach (TechNodeStatus s in new[] { TechNodeStatus.Built, TechNodeStatus.Available, TechNodeStatus.Locked, TechNodeStatus.MissionLocked })
        {
            int sw = BitmapFont.LineHeight(u, FontWeight.Regular);
            Shapes.Fill(sb, _s.Pixel, new Rectangle(x, y, sw, sw), ColourOf(s));
            string word = TechTreeText.Status(s);
            Text(sb, word, x + sw + 2 * u, y, Color.White);
            x += sw + 2 * u + BitmapFont.Measure(word, u, FontWeight.Regular) + 8 * u;
        }
    }

    private void DrawTooltip(SpriteBatch sb)
    {
        UiUnit u = _s.UiScale;
        IReadOnlyList<string> lines = TechTreeText.Lines(_model.Nodes[_hover], _status[_hover], CameoNames.Display);
        int lh = BitmapFont.LineHeight(u, FontWeight.Regular);
        int pad = 4 * u;
        int maxW = Math.Max(40 * u, Math.Min(_s.Viewport.Width / 2, 140 * u));
        var wrapped = new List<string>();
        foreach (string l in lines) Wrap(l, maxW, wrapped);
        int w = 0;
        foreach (string l in wrapped) w = Math.Max(w, BitmapFont.Measure(l, u, FontWeight.Regular));
        var box = new Rectangle(_mouse.X + 12 * u, _mouse.Y + 12 * u, w + 2 * pad, wrapped.Count * lh + 2 * pad);
        if (box.Right > _s.Viewport.Width) box.X = Math.Max(0, _mouse.X - 12 * u - box.Width);
        if (box.Bottom > _s.Viewport.Height) box.Y = Math.Max(0, _s.Viewport.Height - box.Height);
        Shapes.Fill(sb, _s.Pixel, box, new Color(0x10, 0x12, 0x10, 245));
        Shapes.Outline(sb, _s.Pixel, box, new Color(0xB0, 0xB0, 0xB0));
        for (int i = 0; i < wrapped.Count; i++)
            Text(sb, wrapped[i], box.X + pad, box.Y + pad + i * lh, i == 0 ? Color.White : new Color(0xD0, 0xD0, 0xD0), i == 0 ? FontWeight.Bold : FontWeight.Regular);
    }

    // Word wrap of one tooltip line to a pixel width.
    private void Wrap(string text, int width, List<string> into)
    {
        UiUnit u = _s.UiScale;
        string current = "";
        foreach (string word in text.Split(' '))
        {
            string trial = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && BitmapFont.Measure(trial, u, FontWeight.Regular) > width) { into.Add(current); current = word; }
            else current = trial;
        }
        into.Add(current);
    }

    private void Text(SpriteBatch sb, string text, int x, int y, Color colour, FontWeight weight = FontWeight.Regular) =>
        BitmapFont.Draw(sb, _s.Pixel, text, new Vector2(x, y), colour, _s.UiScale, weight);
}
