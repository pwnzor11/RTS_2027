// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Archive;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;
using Rts.Sim.Rules;

namespace Rts.Game.Screens;

/// <summary>
/// The Archive (M3-17): the in-game lore and codex. Category tabs on the left, the entries of the tab beside them
/// (locked ones show as "???" with an unlock hint), and a reading pane with title, a slowly turning sprite preview,
/// live stats from the rules and scrollable body text. The pane is themed by the entry's faction. Keyboard: Tab,
/// Shift+Tab, Left and Right change section; Up and Down change entry; Page Up, Page Down, Home, End scroll the text;
/// Esc leaves. Mouse: click a section or entry; the wheel scrolls the list or the text under the cursor.
/// </summary>
public sealed class ArchiveScreen : IScreen
{
    private const int TicksPerSecond = 20;
    private static readonly Color Backdrop = new(0x0E, 0x10, 0x0C);
    private static readonly Color SelectedFill = new(0x38, 0x4A, 0x38);
    private static readonly Color HoverFill = new(0x26, 0x2E, 0x24);

    private readonly GameServices _s;
    private ArchiveCatalog _catalog = ArchiveCatalog.Empty;
    private ArchiveBrowser _browser;
    private SimRules? _rules;
    private ArchiveSprites? _sprites;
    private string _problem = "";
    private Point _mouse;
    private double _elapsed;
    private int _tick;
    private int _bodyScroll;
    private int _listTop;
    private (ArchiveCategory Tab, int Selected) _lastSelection = (ArchiveCategory.History, -1);
    private Content? _content;
    private Geometry _geo = new();

    /// <summary>Creates the screen. The data is read when the screen is entered.</summary>
    public ArchiveScreen(GameServices s)
    {
        _s = s;
        _browser = new ArchiveBrowser(_catalog, new ArchiveProgress());
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
        string dataDir = Path.GetDirectoryName(Path.GetFullPath(_s.RulesDir)) ?? ".";
        try
        {
            _catalog = ArchiveCatalog.Load(ArchiveCatalog.DirectoryFor(_s.RulesDir));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _catalog = ArchiveCatalog.Empty;
            _problem = "The Archive data could not be read: " + e.Message;
        }
        try
        {
            _rules = RulesLoader.Load(_s.RulesDir).Sim;
        }
        catch (Exception e) when (e is RulesException or IOException or UnauthorizedAccessException)
        {
            _rules = null;   // entries still read, only the live stats block is missing
        }
        _browser = new ArchiveBrowser(_catalog, ArchiveProgress.Load());
        _sprites = new ArchiveSprites(_s.Graphics, Path.Combine(dataDir, "sprites"), _rules);
        _content = null;
        _bodyScroll = 0;
        _listTop = 0;
    }

    /// <inheritdoc />
    public void Exit()
    {
        _sprites?.Dispose();
        _sprites = null;
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        _elapsed += t.ElapsedGameTime.TotalSeconds;
        _tick = (int)(_elapsed * TicksPerSecond);

        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Pop();
            return;
        }

        bool shift = input.IsDown(Keys.LeftShift) || input.IsDown(Keys.RightShift);
        if (input.ConsumePressed(Keys.Tab)) _browser.MoveTab(shift ? -1 : 1);
        if (input.ConsumePressed(Keys.Right)) _browser.MoveTab(1);
        if (input.ConsumePressed(Keys.Left)) _browser.MoveTab(-1);
        if (input.ConsumePressed(Keys.Down)) _browser.MoveEntry(1);
        if (input.ConsumePressed(Keys.Up)) _browser.MoveEntry(-1);

        _geo = Measure();
        int page = Math.Max(1, _geo.BodyView.Height * 9 / 10);
        if (input.ConsumePressed(Keys.PageDown)) _bodyScroll += page;
        if (input.ConsumePressed(Keys.PageUp)) _bodyScroll -= page;
        if (input.ConsumePressed(Keys.Home)) _bodyScroll = 0;
        if (input.ConsumePressed(Keys.End)) _bodyScroll = int.MaxValue / 2;

        int wheel = input.WheelDelta / 120;
        if (wheel != 0)
        {
            if (_geo.List.Contains(_mouse)) _listTop -= wheel * 2;
            else _bodyScroll -= wheel * 3 * _geo.LineStep;
            input.ConsumeWheel();
        }

        if (input.LeftPressed)
        {
            for (int i = 0; i < _geo.Tabs.Length; i++)
            {
                if (!_geo.Tabs[i].Contains(_mouse)) continue;
                _browser.Tab = _browser.AllTabs[i];
                input.ConsumeLeft();
                break;
            }
            if (input.LeftPressed && _geo.List.Contains(_mouse))
            {
                int row = _listTop + (_mouse.Y - _geo.List.Y) / _geo.RowHeight;
                if (row >= 0 && row < _browser.Entries.Count)
                {
                    _browser.Selected = row;
                    input.ConsumeLeft();
                }
            }
        }

        // A new selection (key or click) starts at the top of the text and is scrolled into view in the list.
        (ArchiveCategory Tab, int Selected) sel = (_browser.Tab, _browser.Selected);
        if (sel != _lastSelection)
        {
            _lastSelection = sel;
            _bodyScroll = 0;
            _listTop = Math.Min(_listTop, sel.Selected);
            if (sel.Selected >= _listTop + _geo.ListRows) _listTop = sel.Selected - _geo.ListRows + 1;
        }
        _listTop = Math.Clamp(_listTop, 0, Math.Max(0, _browser.Entries.Count - _geo.ListRows));
        _bodyScroll = Math.Max(0, _bodyScroll);
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Texture2D px = _s.Pixel;
        int scale = _s.UiScale;
        Shapes.Fill(sb, px, new Rectangle(0, 0, _s.Viewport.Width, _s.Viewport.Height), Backdrop);
        _geo = Measure();
        ArchiveTheme neutral = ArchiveTheme.Neutral;

        // Header.
        Text(sb, "ARCHIVE", _geo.Margin, _geo.Margin, neutral.Title, FontWeight.Bold);
        int titleW = BitmapFont.Measure("ARCHIVE", scale, FontWeight.Bold);
        string keys = "Tab: section   Up/Down: entry   PgUp/PgDn/wheel: scroll   Esc: back";
        int keysX = _geo.Margin * 2 + titleW;
        Text(sb, Fit(keys, _s.Viewport.Width - keysX - _geo.Margin, FontWeight.Regular), keysX, _geo.Margin, neutral.Dim);

        DrawTabs(sb);
        DrawList(sb);
        DrawPane(sb);
    }

    private void DrawTabs(SpriteBatch sb)
    {
        ArchiveTheme neutral = ArchiveTheme.Neutral;
        for (int i = 0; i < _geo.Tabs.Length; i++)
        {
            Rectangle r = _geo.Tabs[i];
            ArchiveCategory tab = _browser.AllTabs[i];
            bool selected = tab == _browser.Tab;
            if (selected) Shapes.Fill(sb, _s.Pixel, r, SelectedFill);
            else if (r.Contains(_mouse)) Shapes.Fill(sb, _s.Pixel, r, HoverFill);
            (int open, int total) = _browser.Count(tab);
            string count = $"{open}/{total}";
            int y = r.Y + (r.Height - Line(FontWeight.Regular)) / 2;
            Text(sb, tab.ToString(), r.X + _s.UiScale * 2, y, selected ? Color.White : neutral.Text);
            Text(sb, count, r.Right - BitmapFont.Measure(count, _s.UiScale) - _s.UiScale * 2, y, neutral.Dim);
        }
    }

    private void DrawList(SpriteBatch sb)
    {
        int scale = _s.UiScale;
        Rectangle list = _geo.List;
        Shapes.Fill(sb, _s.Pixel, list, ArchiveTheme.Neutral.Panel);
        Shapes.Outline(sb, _s.Pixel, list, ArchiveTheme.Neutral.Border);
        if (_browser.Entries.Count == 0)
        {
            string msg = _problem.Length > 0 ? _problem : "Nothing here yet.";
            Text(sb, Fit(msg, list.Width - 4 * scale, FontWeight.Regular), list.X + 2 * scale, list.Y + 2 * scale, ArchiveTheme.Neutral.Dim);
            return;
        }

        int lh = Line(FontWeight.Regular);
        for (int row = 0; row < _geo.ListRows; row++)
        {
            int index = _listTop + row;
            if (index >= _browser.Entries.Count) break;
            ArchiveEntry e = _browser.Entries[index];
            bool unlocked = _browser.IsUnlocked(e);
            ArchiveTheme theme = ArchiveTheme.For(e.Faction);
            var r = new Rectangle(list.X + scale, list.Y + row * _geo.RowHeight, list.Width - 2 * scale, _geo.RowHeight);
            if (index == _browser.Selected) Shapes.Fill(sb, _s.Pixel, r, SelectedFill);
            else if (r.Contains(_mouse)) Shapes.Fill(sb, _s.Pixel, r, HoverFill);
            Shapes.Fill(sb, _s.Pixel, new Rectangle(r.X, r.Y + scale, 2 * scale, r.Height - 2 * scale),
                unlocked ? theme.Border : ArchiveTheme.Neutral.Accent);

            int tx = r.X + 4 * scale;
            int width = r.Width - 5 * scale;
            string title = unlocked ? e.Title : "???";
            string sub = unlocked ? e.Subtitle : _browser.Hint(e);
            Text(sb, Fit(title, width, FontWeight.Regular), tx, r.Y + scale, unlocked ? Color.White : ArchiveTheme.Neutral.Dim);
            Text(sb, Fit(sub, width, FontWeight.Regular), tx, r.Y + scale + lh, unlocked ? theme.Dim : ArchiveTheme.Neutral.Accent);
        }
    }

    private void DrawPane(SpriteBatch sb)
    {
        Texture2D px = _s.Pixel;
        int scale = _s.UiScale;
        ArchiveEntry? entry = _browser.Current;
        ArchiveTheme theme = entry is null ? ArchiveTheme.Neutral : ArchiveTheme.For(entry.Faction);
        Rectangle pane = _geo.Pane;
        Shapes.Fill(sb, px, pane, theme.Panel);
        DrawBorder(sb, pane, theme);
        if (entry is null) return;

        bool unlocked = _browser.IsUnlocked(entry);
        Content content = ContentFor(entry, unlocked);
        int pad = _geo.Pad;
        int x = pane.X + pad;
        int y = pane.Y + pad;
        int inner = pane.Width - 2 * pad;

        // Title, subtitle and the rule under them.
        string title = unlocked ? entry.Title : "???";
        if (theme.Stencil) title = title.ToUpperInvariant();
        Text(sb, Fit(title, inner, FontWeight.Bold), x, y, theme.Title, FontWeight.Bold);
        y += Line(FontWeight.Bold);
        string sub = unlocked ? entry.Subtitle : _browser.Hint(entry);
        if (sub.Length > 0)
        {
            Text(sb, Fit(sub, inner, FontWeight.Regular), x, y, unlocked ? theme.Dim : theme.Emphasis);
            y += Line(FontWeight.Regular);
        }
        if (content.Note.Count > 0)
        {
            y += scale;
            int noteH = content.Note.Count * Line(FontWeight.Regular);
            Shapes.Fill(sb, px, new Rectangle(x, y, Math.Max(1, scale), noteH), theme.Accent);
            foreach (ArchiveMarkup.Line line in content.Note)
            {
                DrawWords(sb, line, x + 3 * scale, y, theme, theme.Stencil ? theme.Dim : theme.Emphasis);
                y += Line(FontWeight.Regular);
            }
        }
        y += scale * 2;
        DrawRule(sb, new Rectangle(x, y, inner, 2 * scale), theme);
        y += 2 * scale + _geo.Pad;

        // Preview and stats.
        if (_geo.HeaderBlockHeight > 0)
        {
            var box = new Rectangle(x, y, _geo.PreviewBox, _geo.PreviewBox);
            Shapes.Fill(sb, px, box, new Color(0, 0, 0, 90));
            Shapes.Outline(sb, px, box, theme.Accent);
            if (!unlocked)
            {
                string q = "???";
                Text(sb, q, box.Center.X - BitmapFont.Measure(q, scale, FontWeight.Bold) / 2, box.Center.Y - Line(FontWeight.Bold) / 2,
                    theme.Dim, FontWeight.Bold);
            }
            else if (entry.PreviewSprite.Length > 0)
            {
                _sprites?.Draw(sb, entry.PreviewSprite, Shrink(box, 2 * scale), _tick, PlayerColours.Base(theme.House));
            }

            int sx = box.Right + pad;
            int sy = y;
            int labelW = content.StatLabelWidth;
            foreach ((string label, IReadOnlyList<ArchiveMarkup.Line> lines) in content.Stats)
            {
                Text(sb, label, sx, sy, theme.Dim);
                foreach (ArchiveMarkup.Line line in lines)
                {
                    DrawWords(sb, line, sx + labelW, sy, theme, theme.Text);
                    sy += Line(FontWeight.Regular);
                }
                if (lines.Count == 0) sy += Line(FontWeight.Regular);
            }
            y += _geo.HeaderBlockHeight + pad;
        }

        // Body.
        Rectangle view = _geo.BodyView;
        int step = _geo.LineStep;
        int contentH = content.BodyHeight(step, Line(FontWeight.Regular));
        _bodyScroll = Math.Clamp(_bodyScroll, 0, Math.Max(0, contentH - view.Height));
        int off = 0;
        foreach (ArchiveMarkup.Line line in content.Body)
        {
            if (line.GapBefore) off += step / 2;
            if (line.Heading && off > 0 && !line.GapBefore) off += step / 3;
            int ly = view.Y + off - _bodyScroll;
            if (ly >= view.Y && ly + step <= view.Bottom) DrawWords(sb, line, view.X, ly, theme, theme.Text);
            off += step;
        }
        if (contentH > view.Height)
        {
            var track = new Rectangle(view.Right + scale, view.Y, Math.Max(2, scale), view.Height);
            Shapes.Fill(sb, px, track, Shapes.Alpha(theme.Accent, 80));
            int thumbH = Math.Max(4 * scale, track.Height * view.Height / contentH);
            int thumbY = track.Y + (track.Height - thumbH) * _bodyScroll / Math.Max(1, contentH - view.Height);
            Shapes.Fill(sb, px, new Rectangle(track.X, thumbY, track.Width, thumbH), theme.Border);
        }
    }

    private void DrawWords(SpriteBatch sb, ArchiveMarkup.Line line, int x, int y, ArchiveTheme theme, Color text)
    {
        foreach ((string word, bool emphasis, int dx) in line.Words)
        {
            Color c = line.Heading ? theme.Title : emphasis ? theme.Emphasis : text;
            Text(sb, word, x + dx, y, c, emphasis || line.Heading ? FontWeight.Bold : FontWeight.Regular);
        }
    }

    private void DrawBorder(SpriteBatch sb, Rectangle pane, ArchiveTheme theme)
    {
        int scale = _s.UiScale;
        if (theme.Stencil)
        {
            Shapes.DashedOutline(sb, _s.Pixel, pane, theme.Border, Math.Max(1, scale), 6 * scale, 2 * scale);
            return;
        }
        Shapes.Outline(sb, _s.Pixel, pane, theme.Border, Math.Max(1, scale / 2));
        if (theme.House != 1) return;   // the double border and rivets are the Directorate's
        Rectangle inset = Shrink(pane, 2 * scale);
        Shapes.Outline(sb, _s.Pixel, inset, theme.Accent, Math.Max(1, scale / 2));
        int rivet = Math.Max(2, scale);
        foreach (Point corner in new[] { inset.Location, new Point(inset.Right - rivet, inset.Y), new Point(inset.X, inset.Bottom - rivet),
                     new Point(inset.Right - rivet, inset.Bottom - rivet) })
        {
            Shapes.Fill(sb, _s.Pixel, new Rectangle(corner.X, corner.Y, rivet, rivet), theme.Border);
        }
    }

    private void DrawRule(SpriteBatch sb, Rectangle r, ArchiveTheme theme)
    {
        if (!theme.Stencil)
        {
            Shapes.Fill(sb, _s.Pixel, r, theme.Accent);
            return;
        }
        // Stencil bar: broken into plates with gaps.
        int plate = 10 * _s.UiScale;
        int gap = 2 * _s.UiScale;
        for (int x = r.X; x < r.Right; x += plate + gap)
        {
            Shapes.Fill(sb, _s.Pixel, new Rectangle(x, r.Y, Math.Min(plate, r.Right - x), r.Height), theme.Accent);
        }
    }

    // ---- content (wrapped text) --------------------------------------------------------------------------------

    private Content ContentFor(ArchiveEntry entry, bool unlocked)
    {
        int width = _geo.BodyView.Width;
        int statsWidth = _geo.StatsWidth;
        if (_content is { } c && c.Id == entry.Id && c.Unlocked == unlocked && c.BodyWidth == width && c.StatsWidth == statsWidth
            && c.NoteWidth == _geo.Inner && c.Scale == _s.UiScale)
        {
            return c;
        }

        int scale = _s.UiScale;
        Func<string, bool, int> measure = (t, bold) => BitmapFont.Measure(t, scale, bold ? FontWeight.Bold : FontWeight.Regular);
        string note = unlocked ? entry.FieldNote : "";
        int noteWidth = Math.Max(1, _geo.Inner - 3 * scale);
        IReadOnlyList<ArchiveMarkup.Line> noteLines = note.Length == 0 ? Array.Empty<ArchiveMarkup.Line>()
            : ArchiveMarkup.Wrap(ArchiveMarkup.Parse(note), noteWidth, measure);
        string bodyText = unlocked ? (entry.Body.Length > 0 ? entry.Body : "[no text]") : "This entry is locked. " + _browser.Hint(entry);
        IReadOnlyList<ArchiveMarkup.Line> body = ArchiveMarkup.Wrap(ArchiveMarkup.Parse(bodyText), width, measure);

        var stats = new List<(string, IReadOnlyList<ArchiveMarkup.Line>)>();
        int labelW = 0;
        if (unlocked && entry.HasStats && _rules is not null)
        {
            IReadOnlyList<ArchiveStatLine> rows = ArchiveStats.For(_rules, entry.Id);
            foreach (ArchiveStatLine row in rows) labelW = Math.Max(labelW, measure(row.Label, false));
            labelW += 3 * scale;
            foreach (ArchiveStatLine row in rows)
            {
                // Stat values are plain text: escape nothing, markup characters do not occur in rules ids or numbers.
                stats.Add((row.Label, ArchiveMarkup.Wrap(ArchiveMarkup.Parse(row.Value), Math.Max(1, statsWidth - labelW), measure)));
            }
        }
        _content = new Content(entry.Id, unlocked, width, statsWidth, scale, _geo.Inner, noteLines, body, stats, labelW);
        return _content;
    }

    private sealed record Content(string Id, bool Unlocked, int BodyWidth, int StatsWidth, int Scale, int NoteWidth,
        IReadOnlyList<ArchiveMarkup.Line> Note, IReadOnlyList<ArchiveMarkup.Line> Body, IReadOnlyList<(string Label, IReadOnlyList<ArchiveMarkup.Line> Lines)> Stats,
        int StatLabelWidth)
    {
        public int StatsHeight(int lineH) => Stats.Sum(s => Math.Max(1, s.Lines.Count)) * lineH;

        public int BodyHeight(int step, int lineH)
        {
            int h = 0;
            foreach (ArchiveMarkup.Line line in Body)
            {
                if (line.GapBefore) h += step / 2;
                if (line.Heading && h > 0 && !line.GapBefore) h += step / 3;
                h += step;
            }
            return h;
        }
    }

    // ---- geometry -----------------------------------------------------------------------------------------------

    private sealed record Geometry
    {
        public int Margin { get; init; }
        public int Pad { get; init; }
        public int Inner { get; init; }
        public Rectangle[] Tabs { get; init; } = Array.Empty<Rectangle>();
        public Rectangle List { get; init; }
        public int RowHeight { get; init; } = 1;
        public int ListRows { get; init; } = 1;
        public Rectangle Pane { get; init; }
        public int PreviewBox { get; init; }
        public int StatsWidth { get; init; }
        public int HeaderBlockHeight { get; init; }
        public Rectangle BodyView { get; init; }
        public int LineStep { get; init; } = 1;
    }

    private Geometry Measure()
    {
        int scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        int margin = 4 * scale;
        int pad = 3 * scale;
        int lh = Line(FontWeight.Regular);
        int top = margin + Line(FontWeight.Bold) + margin;
        int bottom = vp.Height - margin;

        int tabW = 0;
        foreach (ArchiveCategory tab in _browser.AllTabs)
        {
            tabW = Math.Max(tabW, BitmapFont.Measure(tab + "  00/00", scale));
        }
        tabW += 6 * scale;
        int tabH = lh + 5 * scale;
        var tabs = new Rectangle[_browser.AllTabs.Count];
        for (int i = 0; i < tabs.Length; i++) tabs[i] = new Rectangle(margin, top + i * (tabH + scale), tabW, tabH);

        int listX = margin + tabW + margin;
        int listW = Math.Clamp(vp.Width / 4, 28 * scale * 6 / 2, 70 * scale * 6 / 2);
        int rowH = 2 * lh + 3 * scale;
        var list = new Rectangle(listX, top, listW, Math.Max(rowH, bottom - top));
        int listRows = Math.Max(1, list.Height / rowH);

        int paneX = list.Right + margin;
        var pane = new Rectangle(paneX, top, Math.Max(100, vp.Width - paneX - margin), Math.Max(rowH, bottom - top));
        int inner = pane.Width - 2 * pad;

        // Header block: preview square (when the entry has a preview or stats) with stats to its right.
        ArchiveEntry? entry = _browser.Current;
        bool unlocked = entry is not null && _browser.IsUnlocked(entry);
        bool hasHeader = entry is not null && (!unlocked ? entry.PreviewSprite.Length > 0 || entry.HasStats
            : entry.PreviewSprite.Length > 0 || (entry.HasStats && _rules is not null));
        int box = hasHeader ? Math.Min(40 * scale, Math.Max(16 * scale, inner / 3)) : 0;
        int statsWidth = Math.Max(1, inner - box - pad);
        int headerH = 0;
        if (hasHeader)
        {
            // Stats height needs the wrapped rows, which depend on statsWidth; use the cached content when it matches.
            int statsH = _content is { } c && entry is not null && c.Id == entry.Id && c.StatsWidth == statsWidth && c.Scale == scale
                ? c.StatsHeight(lh) : 0;
            headerH = Math.Max(box, statsH);
        }

        int noteH = _content is { } nc && entry is not null && nc.Id == entry.Id && nc.NoteWidth == inner && nc.Scale == scale && nc.Unlocked == unlocked
            ? (nc.Note.Count > 0 ? nc.Note.Count * lh + scale : 0) : 0;
        int titleBlock = noteH + pad + Line(FontWeight.Bold) + (entry is not null && (!unlocked || entry.Subtitle.Length > 0) ? lh : 0)
            + 2 * scale + 2 * scale + pad;
        int bodyY = pane.Y + titleBlock + (hasHeader ? headerH + pad : 0);
        int scrollbar = 4 * scale;
        var body = new Rectangle(pane.X + pad, bodyY, Math.Max(1, inner - scrollbar), Math.Max(1, pane.Bottom - pad - bodyY));
        return new Geometry
        {
            Margin = margin, Pad = pad, Inner = inner, Tabs = tabs, List = list, RowHeight = rowH, ListRows = listRows, Pane = pane,
            PreviewBox = box, StatsWidth = statsWidth, HeaderBlockHeight = headerH, BodyView = body, LineStep = lh + scale,
        };
    }

    // ---- text helpers -------------------------------------------------------------------------------------------

    private static Rectangle Shrink(Rectangle r, int by) => new(r.X + by, r.Y + by, Math.Max(0, r.Width - 2 * by), Math.Max(0, r.Height - 2 * by));

    private int Line(FontWeight weight) => BitmapFont.LineHeight(_s.UiScale, weight);

    private void Text(SpriteBatch sb, string text, int x, int y, Color colour, FontWeight weight = FontWeight.Regular) =>
        BitmapFont.Draw(sb, _s.Pixel, text, new Vector2(x, y), colour, _s.UiScale, weight);

    /// <summary>Cuts text to fit a pixel width, ending with "..." when it was cut.</summary>
    private string Fit(string text, int width, FontWeight weight)
    {
        int scale = _s.UiScale;
        if (BitmapFont.Measure(text, scale, weight) <= width) return text;
        int n = text.Length;
        while (n > 0 && BitmapFont.Measure(text[..n] + "...", scale, weight) > width) n--;
        return n == 0 ? "" : text[..n] + "...";
    }
}
