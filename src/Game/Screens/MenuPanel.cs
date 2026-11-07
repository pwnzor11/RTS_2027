// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Ui.Fonts;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
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
    private static readonly Color SelectedFill = new(0x38, 0x4A, 0x38);
    private static readonly Color SelectedText = Color.White;
    private static readonly Color ButtonFill = new(0x2A, 0x2E, 0x26);
    private static readonly Color ButtonHoverFill = new(0x58, 0x6E, 0x50);
    private const int MinWidthChars = 34;

    private readonly string _title;
    private readonly IReadOnlyList<MenuItem> _items;
    private Layout _layout = new(Rectangle.Empty, Array.Empty<Rectangle>(), Array.Empty<IReadOnlyList<Rectangle>>());

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

    /// <summary>Handles keyboard and mouse for this frame and consumes what it uses.</summary>
    public void Update(InputFrame input, Viewport viewport, int scale)
    {
        _layout = Measure(viewport, scale);
        int count = _items.Count;
        if (count == 0) return;

        if (input.ConsumePressed(Keys.Down)) Selected = (Selected + 1) % count;
        if (input.ConsumePressed(Keys.Up)) Selected = (Selected + count - 1) % count;
        if (input.ConsumePressed(Keys.Left)) _items[Selected].Adjust?.Invoke(-1);
        if (input.ConsumePressed(Keys.Right)) _items[Selected].Adjust?.Invoke(1);
        if (input.ConsumePressed(Keys.Enter)) _items[Selected].Activate?.Invoke();

        Point mouse = input.MousePosition;
        if (input.MouseDelta != Point.Zero)
        {
            for (int i = 0; i < count; i++)
                if (_layout.Rows[i].Contains(mouse)) Selected = i;
        }

        if (input.LeftPressed)
        {
            for (int i = 0; i < count; i++)
            {
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

    /// <summary>Draws the panel. The sprite batch is already begun.</summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, Viewport viewport, int scale, Point mouse)
    {
        Layout layout = Measure(viewport, scale);
        _layout = layout;
        Shapes.Fill(sb, pixel, layout.Panel, PanelColour);
        Shapes.Outline(sb, pixel, layout.Panel, BorderColour, Math.Max(1, scale / 2));
        BitmapFont.Draw(sb, pixel, _title, new Vector2(layout.Panel.X + Pad(scale), layout.Panel.Y + Pad(scale)), TitleColour, scale);

        for (int i = 0; i < _items.Count; i++)
        {
            Rectangle row = layout.Rows[i];
            bool selected = i == Selected;
            if (selected) Shapes.Fill(sb, pixel, row, SelectedFill);
            string text = (selected ? "> " : "  ") + _items[i].Text();
            int textY = row.Y + (row.Height - BitmapFont.LineHeight(scale, FontWeight.Regular)) / 2;
            BitmapFont.Draw(sb, pixel, text, new Vector2(row.X + scale * 2, textY), selected ? SelectedText : TextColour, scale);

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

    private static int Pad(int scale) => 4 * scale;

    private static int RowHeight(int scale) => BitmapFont.LineHeight(scale, FontWeight.Regular) + 5 * scale;

    /// <summary>The height of the panel in screen pixels at <paramref name="scale"/> (for callers that reserve room for it).</summary>
    public int HeightAt(int scale) => Pad(scale) + RowHeight(scale) + Pad(scale) / 2 + _items.Count * (RowHeight(scale) + scale) + Pad(scale);

    private Layout Measure(Viewport viewport, int scale)
    {
        int pad = Pad(scale);
        int rowH = RowHeight(scale);
        int buttonH = rowH - 2 * scale;
        int width = BitmapFont.Measure(_title, scale);
        var rowButtonWidths = new List<int[]>();
        foreach (MenuItem item in _items)
        {
            int[] widths = item.Buttons.Select(b => BitmapFont.Measure(b.Label, scale) + 4 * scale).ToArray();
            rowButtonWidths.Add(widths);
            int buttons = widths.Sum() + widths.Length * scale;
            width = Math.Max(width, BitmapFont.Measure("> " + item.Text(), scale) + 4 * scale + buttons);
        }
        width = Math.Max(width, MinWidthChars * BitmapFont.Advance * scale / 2) + 2 * pad;

        int height = pad + rowH + pad / 2 + _items.Count * (rowH + scale) + pad;
        var panel = new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2 + OffsetY, width, height);
        var rows = new Rectangle[_items.Count];
        var buttonRects = new IReadOnlyList<Rectangle>[_items.Count];
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
            y += rowH + scale;
        }
        return new Layout(panel, rows, buttonRects);
    }

    private sealed record Layout(Rectangle Panel, Rectangle[] Rows, IReadOnlyList<Rectangle>[] Buttons);
}
