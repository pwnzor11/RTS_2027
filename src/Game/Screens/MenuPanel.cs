// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Fonts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Render;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>A small clickable button drawn at the right end of a menu row (for example "-" and "+").</summary>
/// <param name="Label">Button text.</param>
/// <param name="Click">Runs when the button is clicked.</param>
public sealed record MenuButton(string Label, Action Click);

/// <summary>One row of a <see cref="MenuPanel"/>.</summary>
public sealed class MenuItem
{
    /// <summary>Creates a row.</summary>
    /// <param name="text">The row text, asked each frame so it can show a changing value.</param>
    /// <param name="activate">Runs on Enter or a click on the row.</param>
    /// <param name="adjust">Runs with -1 or +1 on Left or Right.</param>
    /// <param name="buttons">Small buttons at the right end of the row.</param>
    public MenuItem(Func<string> text, Action? activate = null, Action<int>? adjust = null, params MenuButton[] buttons)
    {
        Text = text;
        Activate = activate;
        Adjust = adjust;
        Buttons = buttons;
    }

    /// <summary>The row text.</summary>
    public Func<string> Text { get; }

    /// <summary>Enter or click action, or null.</summary>
    public Action? Activate { get; }

    /// <summary>Left/Right action, or null.</summary>
    public Action<int>? Adjust { get; }

    /// <summary>Mini buttons, left to right.</summary>
    public IReadOnlyList<MenuButton> Buttons { get; }

    /// <summary>
    /// Every text this row can show, when it changes while the screen is open. The panel is sized for the widest of them, so it keeps
    /// one width however the row changes (S18 UX-LM-03: the panel used to follow the current text and move under the cursor).
    /// </summary>
    public IReadOnlyList<string>? WidthTexts { get; init; }

    /// <summary>The colour of a small swatch drawn between the text and the buttons (the player colour of a setup slot): null for no swatch on this row, or a function that returns null while there is nothing to show (its room is kept).</summary>
    public Func<Color?>? Swatch { get; init; }

    /// <summary>
    /// True for a line that only informs (a help line, an empty-list message): drawn dim, never highlighted, skipped by Up/Down and
    /// ignored by the mouse (S18 UX-LM-12).
    /// </summary>
    public bool Info { get; init; }
}

/// <summary>
/// A centred dark panel with a title and rows, driven by keyboard (Up/Down/Enter, Left/Right to adjust) and mouse
/// (hover selects, click activates). Uses only <see cref="BitmapFont"/> and <see cref="Shapes"/>. The panel is a UI
/// region: while the cursor is over it the mouse never reaches screens below.
/// </summary>
public sealed class MenuPanel : IUiRegion
{
    private static readonly Color PanelColour = new(0x16, 0x18, 0x14, 240);
    private static readonly Color BorderColour = new(0x6A, 0x72, 0x60);
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color TextColour = new(0xE2, 0xE4, 0xD8);
    private static readonly Color InfoText = new(0x9A, 0x9E, 0x90);
    private static readonly Color SelectedFill = new(0x38, 0x4A, 0x38);
    private static readonly Color SelectedText = Color.White;
    private static readonly Color ButtonFill = new(0x2A, 0x2E, 0x26);
    private static readonly Color ButtonHoverFill = new(0x58, 0x6E, 0x50);
    private const int MinWidthChars = 34;

    private readonly string _title;
    private readonly IReadOnlyList<MenuItem> _items;
    private Layout _layout = new(Rectangle.Empty, Array.Empty<Rectangle>(), Array.Empty<IReadOnlyList<Rectangle>>(), Array.Empty<Rectangle>(), Array.Empty<Rectangle>(), 1);

    /// <summary>Creates the panel.</summary>
    public MenuPanel(string title, IReadOnlyList<MenuItem> items)
    {
        _title = title;
        _items = items;
    }

    /// <summary>Pixels the panel is moved down from the centre of the viewport it is given (to sit in a strip of a larger screen).</summary>
    public int OffsetY { get; set; }

    /// <summary>Index of the highlighted row.</summary>
    public int Selected { get; set; }

    /// <inheritdoc />
    public bool HitTest(Point screen) => _layout.Panel.Contains(screen);

    /// <summary>Screen rectangle of a row as laid out by the last Update or Draw.</summary>
    public Rectangle RowRect(int row) => _layout.Rows[row];

    /// <summary>Screen rectangle of a row's mini button as laid out by the last Update or Draw.</summary>
    public Rectangle ButtonRect(int row, int button) => _layout.Buttons[row][button];

    /// <summary>Screen rectangle of the swatch of a row, or an empty rectangle when the row has none, as laid out by the last Update or Draw.</summary>
    public Rectangle SwatchRect(int row) => _layout.Swatches[row];

    /// <summary>Screen rectangle the text of a row may use (left of its swatch and buttons), as laid out by the last Update or Draw.</summary>
    public Rectangle TextRect(int row) => _layout.TextAreas[row];

    /// <summary>The panel rectangle as laid out by the last Update or Draw.</summary>
    public Rectangle PanelRect => _layout.Panel;

    /// <summary>The scale the last layout settled on (the requested one, or smaller when the panel had to shrink to fit the window).</summary>
    public UiUnit LayoutScale => _layout.Scale;

    /// <summary>The text of a row as it is drawn: the row text, cut with "..." when it is wider than <see cref="TextRect"/>.</summary>
    public string ShownText(int row)
    {
        string text = (row == Selected && !_items[row].Info ? "> " : "  ") + _items[row].Text();
        return TextWrap.Ellipsize(text, _layout.TextAreas[row].Width, t => BitmapFont.Measure(t, _layout.Scale));
    }

    /// <summary>Handles keyboard and mouse for this frame and consumes what it uses.</summary>
    public void Update(InputFrame input, Viewport viewport, UiUnit scale)
    {
        scale = Fitted(viewport, scale);
        _layout = Measure(viewport, scale);
        int count = _items.Count;
        if (count == 0) return;

        Selected = Math.Clamp(Selected, 0, count - 1);
        if (_items[Selected].Info) Selected = Step(Selected, 1);
        if (input.ConsumePressed(Keys.Down)) Selected = Step(Selected, 1);
        if (input.ConsumePressed(Keys.Up)) Selected = Step(Selected, -1);
        if (input.ConsumePressed(Keys.Left)) _items[Selected].Adjust?.Invoke(-1);
        if (input.ConsumePressed(Keys.Right)) _items[Selected].Adjust?.Invoke(1);
        if (input.ConsumePressed(Keys.Enter)) _items[Selected].Activate?.Invoke();

        Point mouse = input.MousePosition;
        if (input.MouseDelta != Point.Zero)
        {
            for (int i = 0; i < count; i++)
                if (!_items[i].Info && _layout.Rows[i].Contains(mouse)) Selected = i;
        }

        if (input.LeftPressed)
        {
            for (int i = 0; i < count; i++)
            {
                if (_items[i].Info) continue;
                IReadOnlyList<Rectangle> buttons = _layout.Buttons[i];
                for (int b = 0; b < buttons.Count; b++)
                {
                    if (!buttons[b].Contains(mouse)) continue;
                    Selected = i;
                    _items[i].Buttons[b].Click();
                    input.ConsumeLeft();
                    break;
                }
                if (!input.LeftPressed) break;
                if (_layout.Rows[i].Contains(mouse))
                {
                    Selected = i;
                    _items[i].Activate?.Invoke();
                    input.ConsumeLeft();
                    break;
                }
            }
        }

        input.ConsumeMouseOver(this);
    }

    // The next row from <from> in direction <d> that is not an information line (wrapping); <from> itself when every row is one.
    private int Step(int from, int d)
    {
        int count = _items.Count;
        int at = from;
        for (int k = 0; k < count; k++)
        {
            at = (at + d + count) % count;
            if (!_items[at].Info) return at;
        }
        return from;
    }

    /// <summary>Draws the panel. The sprite batch is already begun.</summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, Viewport viewport, UiUnit scale, Point mouse)
    {
        scale = Fitted(viewport, scale);
        Layout layout = Measure(viewport, scale);
        _layout = layout;
        Shapes.Fill(sb, pixel, layout.Panel, PanelColour);
        Shapes.Outline(sb, pixel, layout.Panel, BorderColour, Math.Max(1, scale / 2));
        BitmapFont.Draw(sb, pixel, _title, new Vector2(layout.Panel.X + Pad(scale), layout.Panel.Y + Pad(scale)), TitleColour, scale);

        for (int i = 0; i < _items.Count; i++)
        {
            Rectangle row = layout.Rows[i];
            bool info = _items[i].Info;
            bool selected = i == Selected && !info;
            if (selected) Shapes.Fill(sb, pixel, row, SelectedFill);
            string text = TextWrap.Ellipsize((selected ? "> " : "  ") + _items[i].Text(), layout.TextAreas[i].Width, t => BitmapFont.Measure(t, scale));
            int textY = row.Y + (row.Height - BitmapFont.LineHeight(scale, FontWeight.Regular)) / 2;
            BitmapFont.Draw(sb, pixel, text, new Vector2(row.X + scale * 2, textY), info ? InfoText : selected ? SelectedText : TextColour, scale);

            if (_items[i].Swatch?.Invoke() is { } swatchColour)
            {
                Rectangle swatch = layout.Swatches[i];
                Shapes.Fill(sb, pixel, swatch, swatchColour);
                Shapes.Outline(sb, pixel, swatch, Color.White);
            }

            for (int b = 0; b < _items[i].Buttons.Count; b++)
            {
                Rectangle button = layout.Buttons[i][b];
                Shapes.Fill(sb, pixel, button, button.Contains(mouse) ? ButtonHoverFill : ButtonFill);
                Shapes.Outline(sb, pixel, button, BorderColour);
                string label = _items[i].Buttons[b].Label;
                int lx = button.X + (button.Width - BitmapFont.Measure(label, scale) + scale) / 2;
                BitmapFont.Draw(sb, pixel, label, new Vector2(lx, textY), TextColour, scale);
            }
        }
    }

    private static int Pad(UiUnit scale) => 4 * scale;

    private static int RowHeight(UiUnit scale) => BitmapFont.LineHeight(scale, FontWeight.Regular) + 5 * scale;

    /// <summary>The height of the panel in screen pixels at <paramref name="scale"/> (for callers that reserve room for it).</summary>
    public int HeightAt(UiUnit scale) => Pad(scale) + RowHeight(scale) + Pad(scale) / 2 + _items.Count * (RowHeight(scale) + scale) + Pad(scale);

    /// <summary>The share of the requested unit a panel may shrink to before it gives up (S18 UX-LM-02: half was not enough for the setup page at 200 % and Extra large text).</summary>
    public const float FloorShare = 0.3f;

    /// <summary>The largest unit not above <paramref name="scale"/> (in 5 % steps) at which the panel, its rows and their buttons fit the window (M11-10h: a tall page at 200 % or Extra large text shrinks instead of running off screen).</summary>
    private UiUnit Fitted(Viewport viewport, UiUnit scale)
    {
        float v = scale.Value, floor = scale.Value * FloorShare;
        while (v > floor && !Fits(viewport, new UiUnit(v))) v -= scale.Value * 0.05f;
        return new UiUnit(Math.Max(v, floor));
    }

    private bool Fits(Viewport viewport, UiUnit scale)
    {
        if (HeightAt(scale) > viewport.Height - 8) return false;
        Layout l = Measure(viewport, scale);
        return l.Panel.Width <= viewport.Width - 8;
    }

    private Layout Measure(Viewport viewport, UiUnit scale)
    {
        int pad = Pad(scale);
        int rowH = RowHeight(scale);
        int buttonH = rowH - 2 * scale;
        int swatchSize = rowH - 4 * scale;
        int swatchSpace = swatchSize + 3 * scale;
        int width = BitmapFont.Measure(_title, scale);
        var rowButtonWidths = new List<int[]>();
        foreach (MenuItem item in _items)
        {
            int[] widths = item.Buttons.Select(b => BitmapFont.Measure(b.Label, scale) + 4 * scale).ToArray();
            rowButtonWidths.Add(widths);
            int buttons = widths.Sum() + widths.Length * scale;
            int text = BitmapFont.Measure("> " + item.Text(), scale);
            if (item.WidthTexts is { } all)
                foreach (string t in all) text = Math.Max(text, BitmapFont.Measure("> " + t, scale));
            width = Math.Max(width, text + 4 * scale + buttons + (item.Swatch is null ? 0 : swatchSpace));
        }
        width = Math.Max(width, MinWidthChars * BitmapFont.Advance * scale / 2) + 2 * pad;

        int height = pad + rowH + pad / 2 + _items.Count * (rowH + scale) + pad;
        var panel = new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2 + OffsetY, width, height);
        var rows = new Rectangle[_items.Count];
        var buttonRects = new IReadOnlyList<Rectangle>[_items.Count];
        var swatches = new Rectangle[_items.Count];
        var textAreas = new Rectangle[_items.Count];
        int y = panel.Y + pad + rowH + pad / 2;
        for (int i = 0; i < _items.Count; i++)
        {
            rows[i] = new Rectangle(panel.X + pad, y, width - 2 * pad, rowH);
            int[] widths = rowButtonWidths[i];
            var rects = new Rectangle[widths.Length];
            int x = rows[i].Right - scale;
            for (int b = widths.Length - 1; b >= 0; b--)
            {
                x -= widths[b];
                rects[b] = new Rectangle(x, y + (rowH - buttonH) / 2, widths[b], buttonH);
                x -= scale;
            }
            buttonRects[i] = rects;
            // The text gets what is left of the swatch (and the buttons); a longer text is cut with "...", never drawn under them.
            int right = widths.Length > 0 ? x : rows[i].Right - scale;
            if (_items[i].Swatch is not null)
            {
                swatches[i] = new Rectangle(right - swatchSize - scale, y + (rowH - swatchSize) / 2, swatchSize, swatchSize);
                right = swatches[i].X - 2 * scale;
            }
            int left = rows[i].X + scale * 2;
            textAreas[i] = new Rectangle(left, y, Math.Max(0, right - left), rowH);
            y += rowH + scale;
        }
        return new Layout(panel, rows, buttonRects, swatches, textAreas, scale);
    }

    private sealed record Layout(Rectangle Panel, Rectangle[] Rows, IReadOnlyList<Rectangle>[] Buttons, Rectangle[] Swatches, Rectangle[] TextAreas, UiUnit Scale);
}
