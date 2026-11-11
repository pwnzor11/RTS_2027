// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>
/// How to Play: a paged guide. Left / Right (or the arrows on screen, or Page Up / Page Down) change page; each page has an
/// illustration drawn from the game's own sprites and a few lines of text. Esc goes back.
/// </summary>
public sealed class HowToPlayScreen : IScreen
{
    private static readonly Color PanelColour = new(0x12, 0x14, 0x10, 238);
    private static readonly Color BorderColour = new(0x6A, 0x72, 0x60);
    private static readonly Color TitleColour = new(0xE8, 0xA8, 0x28);
    private static readonly Color TextColour = new(0xDC, 0xDE, 0xD0);
    private static readonly Color Dim = new(0x90, 0x94, 0x86);
    private static readonly Color Ground = new(0x24, 0x28, 0x20);
    private static readonly Color Green = new(0x5C, 0xD0, 0x5C);

    private readonly GameServices _s;
    private float _seconds;
    private int _page;
    private Point _mouse;

    /// <summary>Creates the guide at the first page.</summary>
    public HowToPlayScreen(GameServices services) => _s = services;

    /// <summary>The page being shown.</summary>
    public int Page => _page;

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
        _seconds += (float)t.ElapsedGameTime.TotalSeconds;
        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(Keys.Back))
        {
            MenuSounds.Play(_s.Audio, MenuSounds.Back);
            _s.Screens.Pop();
            return;
        }

        int count = HowToPlayContent.Pages.Count;
        int step = 0;
        if (input.ConsumePressed(Keys.Right) || input.ConsumePressed(Keys.PageDown) || input.ConsumePressed(Keys.Enter)) step = 1;
        if (input.ConsumePressed(Keys.Left) || input.ConsumePressed(Keys.PageUp)) step = -1;
        if (input.ConsumePressed(Keys.Home)) _page = 0;
        if (input.ConsumePressed(Keys.End)) _page = count - 1;
        if (input.LeftPressed)
        {
            Layout l = Measure();
            if (l.Prev.Contains(_mouse)) step = -1;
            else if (l.Next.Contains(_mouse)) step = 1;
            else if (l.Back.Contains(_mouse)) { _s.Screens.Pop(); input.ConsumeLeft(); return; }
            if (step != 0) input.ConsumeLeft();
        }
        if (step != 0)
        {
            _page = (_page + step + count) % count;
            MenuSounds.Play(_s.Audio, MenuSounds.Move);
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        var scale = _s.UiScale;
        Layout l = Measure();
        Shapes.Fill(sb, _s.Pixel, l.Panel, PanelColour);
        Shapes.Outline(sb, _s.Pixel, l.Panel, BorderColour, Math.Max(1, scale / 2));

        GuidePage page = HowToPlayContent.Pages[_page];
        int pad = 5 * scale;
        BitmapFont.Draw(sb, _s.Pixel, "How to Play", new Vector2(l.Panel.X + pad, l.Panel.Y + pad), Dim, scale);
        BitmapFont.Draw(sb, _s.Pixel, page.Title, new Vector2(l.Panel.X + pad, l.Panel.Y + pad + l.Line + scale), TitleColour, scale, FontWeight.Bold);

        DrawIllustration(sb, page.Art, l.Art, scale);

        int y = l.Text.Y;
        foreach (string bullet in page.Lines)
        {
            List<string> wrapped = TextWrap.Wrap(bullet, l.Text.Width - 4 * scale, t => BitmapFont.Measure(t, scale));
            for (int i = 0; i < wrapped.Count; i++)
            {
                if (y + l.Line > l.Text.Bottom) break;
                if (i == 0) Shapes.Fill(sb, _s.Pixel, new Rectangle(l.Text.X, y + l.Line / 2 - scale, 2 * scale, 2 * scale), TitleColour);
                BitmapFont.Draw(sb, _s.Pixel, wrapped[i], new Vector2(l.Text.X + 4 * scale, y), TextColour, scale);
                y += l.Line;
            }
            y += scale * 2;
        }

        DrawButton(sb, l.Prev, "<", scale);
        DrawButton(sb, l.Next, ">", scale);
        DrawButton(sb, l.Back, "Back", scale);
        string counter = $"{_page + 1} / {HowToPlayContent.Pages.Count}";
        BitmapFont.Draw(sb, _s.Pixel, counter, new Vector2(l.Panel.Center.X - BitmapFont.Measure(counter, scale) / 2, l.Prev.Y + scale), Dim, scale);
    }

    private void DrawButton(SpriteBatch sb, Rectangle r, string label, UiUnit scale)
    {
        Shapes.Fill(sb, _s.Pixel, r, r.Contains(_mouse) ? new Color(0x58, 0x6E, 0x50) : new Color(0x2A, 0x2E, 0x26));
        Shapes.Outline(sb, _s.Pixel, r, BorderColour);
        BitmapFont.Draw(sb, _s.Pixel, label, new Vector2(r.Center.X - BitmapFont.Measure(label, scale) / 2, r.Y + scale), TextColour, scale);
    }

    private sealed record Layout(Rectangle Panel, Rectangle Art, Rectangle Text, Rectangle Prev, Rectangle Next, Rectangle Back, int Line);

    private Layout Measure() => MeasureFor(_s.Viewport, _s.UiScale);

    /// <summary>The Back button's rectangle for a window and scale (public so a test can check that its label fits the box, S18 UX-LM-10).</summary>
    public static Rectangle BackRectFor(Viewport vp, UiUnit scale) => MeasureFor(vp, scale).Back;

    private static Layout MeasureFor(Viewport vp, UiUnit scale)
    {
        int line = BitmapFont.LineHeight(scale, FontWeight.Regular) + scale;
        int width = Math.Min(vp.Width - 8 * scale, 120 * scale * BitmapFont.Advance / 2);
        int height = Math.Min(vp.Height - 8 * scale, 150 * scale * 4);
        var panel = new Rectangle((vp.Width - width) / 2, (vp.Height - height) / 2, width, height);
        int pad = 5 * scale;
        int headerH = pad + 2 * line + 2 * scale;
        int footerH = line + 2 * pad;
        // Wide windows: art on the left, text on the right. Narrow: art above the text.
        bool wide = width >= 90 * scale * BitmapFont.Advance / 2;
        int inner = panel.Height - headerH - footerH;
        Rectangle art, text;
        if (wide)
        {
            int artW = (int)((width - 3 * pad) * 0.42f);
            art = new Rectangle(panel.X + pad, panel.Y + headerH, artW, Math.Min(inner, artW * 3 / 4));
            text = new Rectangle(art.Right + pad, panel.Y + headerH, width - artW - 3 * pad, inner);
        }
        else
        {
            int artH = inner * 2 / 5;
            art = new Rectangle(panel.X + pad, panel.Y + headerH, width - 2 * pad, artH);
            text = new Rectangle(panel.X + pad, art.Bottom + pad, width - 2 * pad, inner - artH - pad);
        }
        int bw = 10 * scale, bh = line;
        int by = panel.Bottom - pad - bh;
        var prev = new Rectangle(panel.Center.X - 30 * scale - bw, by, bw, bh);
        var next = new Rectangle(panel.Center.X + 30 * scale, by, bw, bh);
        int backW = ButtonSizing.WidthFor("Back", scale, 14);   // as wide as its label needs (the label used to poke out of a fixed 14-unit box)
        var back = new Rectangle(panel.Right - pad - backW, by, backW, bh);
        return new Layout(panel, art, text, prev, next, back, line);
    }

    // ---- illustrations ----

    private void DrawIllustration(SpriteBatch sb, GuideArt art, Rectangle area, UiUnit scale)
    {
        Shapes.Fill(sb, _s.Pixel, area, Ground);
        Shapes.Outline(sb, _s.Pixel, area, BorderColour);
        Point c = area.Center;
        float u = Math.Max(0.6f, area.Width / 330f);   // sprite scale that fits the area
        int tick = (int)(_seconds * 20f);
        switch (art)
        {
            case GuideArt.Base:
                Sprite(sb, "auth_conyard", "idle", 0, tick, area.X + area.Width * 0.30f, area.Y + area.Height * 0.78f, u * 0.62f, true);
                Sprite(sb, "auth_power", "idle", 0, tick, area.X + area.Width * 0.62f, area.Y + area.Height * 0.60f, u * 0.62f, true);
                Sprite(sb, "auth_refinery", "idle", 0, tick, area.X + area.Width * 0.66f, area.Y + area.Height * 0.95f, u * 0.62f, true);
                Sprite(sb, "auth_scraper", "idle", 64, tick, area.X + area.Width * 0.38f, area.Y + area.Height * 0.93f, u * 0.8f, false);
                break;
            case GuideArt.Selecting:
            {
                var tanks = new[] { (0.25f, 0.45f), (0.45f, 0.62f), (0.62f, 0.40f), (0.74f, 0.66f) };
                foreach ((float fx, float fy) in tanks)
                    Sprite(sb, "auth_tank", "idle", 64, tick, area.X + area.Width * fx, area.Y + area.Height * fy, u * 0.7f, false);
                var box = new Rectangle(area.X + (int)(area.Width * 0.12f), area.Y + (int)(area.Height * 0.15f), (int)(area.Width * 0.7f), (int)(area.Height * 0.62f));
                Shapes.DashedOutline(sb, _s.Pixel, box, Green, Math.Max(1, scale / 2), 4 * scale, 3 * scale);
                Cursor(sb, new Point(box.Right, box.Bottom), scale);
                break;
            }
            case GuideArt.Orders:
            {
                var from = new Vector2(area.X + area.Width * 0.22f, area.Y + area.Height * 0.62f);
                var to = new Vector2(area.X + area.Width * 0.62f, area.Y + area.Height * 0.40f);
                Sprite(sb, "auth_tank", "idle", 64, tick, from.X, from.Y, u * 0.75f, false);
                Shapes.Line(sb, _s.Pixel, from + new Vector2(0, -4 * scale), to, Green, Math.Max(1, scale / 2));
                Shapes.Circle(sb, _s.Pixel, to, 5 * scale, Green, Math.Max(1, scale / 2), 20);
                Sprite(sb, "auth_turret", "idle", 192, tick, area.X + area.Width * 0.82f, area.Y + area.Height * 0.85f, u * 0.7f, false);
                Shapes.Line(sb, _s.Pixel, from, new Vector2(area.X + area.Width * 0.76f, area.Y + area.Height * 0.78f), new Color(0xD8, 0x50, 0x40), Math.Max(1, scale / 2));
                Cursor(sb, to.ToPoint(), scale);
                break;
            }
            case GuideArt.Building:
            {
                string[] tabs = { "Q STR", "W DEF", "E INF", "R VEH", "T UPG" };
                int tabW = (area.Width - 4 * scale) / 5;
                for (int i = 0; i < tabs.Length; i++)
                {
                    var r = new Rectangle(area.X + 2 * scale + i * tabW, area.Y + 2 * scale, tabW - scale, BitmapFont.LineHeight(scale, FontWeight.Regular) + 2 * scale);
                    Shapes.Fill(sb, _s.Pixel, r, i == 0 ? new Color(0x38, 0x4A, 0x38) : new Color(0x2A, 0x2E, 0x26));
                    Shapes.Outline(sb, _s.Pixel, r, BorderColour);
                    BitmapFont.Draw(sb, _s.Pixel, tabs[i], new Vector2(r.X + 2 * scale, r.Y + scale), TextColour, 1);
                }
                var ghost = new Rectangle(area.X + (int)(area.Width * 0.5f), area.Y + (int)(area.Height * 0.4f), (int)(area.Width * 0.3f), (int)(area.Height * 0.32f));
                Shapes.Fill(sb, _s.Pixel, ghost, new Color(0x5C, 0xD0, 0x5C, 70));
                Shapes.Outline(sb, _s.Pixel, ghost, Green, Math.Max(1, scale / 2));
                Sprite(sb, "auth_power", "idle", 0, tick, area.X + area.Width * 0.18f, area.Y + area.Height * 0.9f, u * 0.55f, true);
                Cursor(sb, ghost.Center, scale);
                break;
            }
            case GuideArt.Harvesting:
            {
                var rnd = new SceneRng(7);
                for (int i = 0; i < 60; i++)
                    Shapes.Fill(sb, _s.Pixel, new Rectangle(area.X + (int)(area.Width * (0.04f + 0.3f * rnd.Next())), area.Y + (int)(area.Height * (0.45f + 0.45f * rnd.Next())), 2 * scale, scale.Whole), new Color(0x78, 0x78, 0x6E));
                Sprite(sb, "auth_scraper", "idle", 64, tick, area.X + area.Width * 0.45f, area.Y + area.Height * 0.8f, u * 0.8f, false);
                Sprite(sb, "auth_refinery", "idle", 0, tick, area.X + area.Width * 0.78f, area.Y + area.Height * 0.92f, u * 0.6f, true);
                Shapes.Line(sb, _s.Pixel, new Vector2(area.X + area.Width * 0.3f, area.Y + area.Height * 0.62f), new Vector2(area.X + area.Width * 0.6f, area.Y + area.Height * 0.62f), Green, Math.Max(1, scale / 2));
                break;
            }
            case GuideArt.Tunnels:
            {
                var a = new Point(area.X + (int)(area.Width * 0.25f), area.Y + (int)(area.Height * 0.38f));
                var b = new Point(area.X + (int)(area.Width * 0.75f), area.Y + (int)(area.Height * 0.72f));
                Shapes.Fill(sb, _s.Pixel, new Rectangle(area.X, area.Y + area.Height / 2, area.Width, area.Height / 2), new Color(0x1A, 0x1C, 0x18));
                Shapes.DashedOutline(sb, _s.Pixel, new Rectangle(Math.Min(a.X, b.X), a.Y, Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)), new Color(0xB0, 0x40, 0x34), Math.Max(1, scale / 2), 3 * scale, 3 * scale);
                foreach (Point p in new[] { a, b })
                {
                    Shapes.Fill(sb, _s.Pixel, new Rectangle(p.X - 8 * scale, p.Y - 4 * scale, 16 * scale, 8 * scale), new Color(0x08, 0x08, 0x08));
                    Shapes.Outline(sb, _s.Pixel, new Rectangle(p.X - 8 * scale, p.Y - 4 * scale, 16 * scale, 8 * scale), new Color(0xB0, 0x40, 0x34), Math.Max(1, scale / 2));
                }
                BitmapFont.Draw(sb, _s.Pixel, "entrance", new Vector2(a.X - 14 * scale, a.Y - 4 * scale - BitmapFont.LineHeight(scale, FontWeight.Regular)), Dim, scale);
                BitmapFont.Draw(sb, _s.Pixel, "entrance", new Vector2(b.X - 14 * scale, b.Y + 5 * scale), Dim, scale);
                break;
            }
            case GuideArt.Upgrades:
            {
                var tab = new Rectangle(area.X + 2 * scale, area.Y + 2 * scale, area.Width / 3, BitmapFont.LineHeight(scale, FontWeight.Regular) + 2 * scale);
                Shapes.Fill(sb, _s.Pixel, tab, new Color(0x38, 0x4A, 0x38));
                Shapes.Outline(sb, _s.Pixel, tab, BorderColour);
                BitmapFont.Draw(sb, _s.Pixel, "T  Upgrades", new Vector2(tab.X + 2 * scale, tab.Y + scale), TextColour, scale);
                for (int i = 0; i < 3; i++)
                {
                    var cameo = new Rectangle(area.X + 3 * scale, tab.Bottom + 3 * scale + i * (tab.Height + 3 * scale), area.Width - 6 * scale, tab.Height);
                    Shapes.Fill(sb, _s.Pixel, cameo, new Color(0x2A, 0x2E, 0x26));
                    Shapes.Outline(sb, _s.Pixel, cameo, i == 0 ? Green : BorderColour);
                    Shapes.Fill(sb, _s.Pixel, new Rectangle(cameo.X + scale, cameo.Bottom - 3 * scale, (cameo.Width - 2 * scale) * (i == 0 ? 6 : 0) / 10, 2 * scale), Green);
                }
                break;
            }
            case GuideArt.Keys:
            {
                string[] keys = { "A", "S", "D", "G", "X", "Q", "W", "E", "R", "T" };
                int size = Math.Min(area.Width / 6, 14 * scale);
                for (int i = 0; i < keys.Length; i++)
                {
                    var r = new Rectangle(area.X + 2 * scale + (i % 5) * (size + 2 * scale), area.Y + 3 * scale + (i / 5) * (size + 3 * scale), size, size);
                    Shapes.Fill(sb, _s.Pixel, r, new Color(0x2A, 0x2E, 0x26));
                    Shapes.Outline(sb, _s.Pixel, r, BorderColour);
                    BitmapFont.Draw(sb, _s.Pixel, keys[i], new Vector2(r.Center.X - BitmapFont.Measure(keys[i], scale) / 2, r.Y + (size - BitmapFont.LineHeight(scale, FontWeight.Regular)) / 2), TitleColour, scale, FontWeight.Bold);
                }
                break;
            }
        }
    }

    private void Sprite(SpriteBatch sb, string id, string state, byte facing, int tick, float x, float y, float scale, bool bottomAnchor)
    {
        if (_s.Sprites is null) return;
        try
        {
            SpriteSheet sheet = _s.Sprites.Sheet(id);
            int frame = sheet.FrameIndex(state, facing, tick);
            var pos = new Vector2(x, y);
            if (bottomAnchor)
            {
                pos.X += (sheet.Origin.X - sheet.FrameW / 2f) * scale;
                pos.Y += (sheet.Origin.Y - sheet.FrameH) * scale;
            }
            sheet.Draw(sb, frame, pos, PlayerColours.Choice(0), scale);
        }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or ArgumentOutOfRangeException)
        {
            // Sprite not available (headless run): the page still has its text.
        }
    }

    private void Cursor(SpriteBatch sb, Point at, UiUnit scale)
    {
        // A small arrow cursor: a triangle made of three short lines.
        var tip = at.ToVector2();
        Shapes.Line(sb, _s.Pixel, tip, tip + new Vector2(0, 10 * scale), Color.White, Math.Max(1, scale.Whole));
        Shapes.Line(sb, _s.Pixel, tip, tip + new Vector2(7 * scale, 7 * scale), Color.White, Math.Max(1, scale.Whole));
        Shapes.Line(sb, _s.Pixel, tip + new Vector2(0, 10 * scale), tip + new Vector2(3 * scale, 7 * scale), Color.White, Math.Max(1, scale.Whole));
    }
}
