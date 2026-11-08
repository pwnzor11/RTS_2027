// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui;

namespace Rts.Game.Screens.Editor;

/// <summary>A button of the editor's immediate-mode UI, rebuilt every frame.</summary>
/// <param name="Rect">Screen rectangle.</param>
/// <param name="Text">Label.</param>
/// <param name="Active">Drawn highlighted (the current tool, brush, ...).</param>
/// <param name="Click">Runs on a left click.</param>
/// <param name="Enabled">False draws it dim and ignores clicks.</param>
public sealed record EditorButton(Rectangle Rect, string Text, bool Active, Action Click, bool Enabled = true);

/// <summary>A caption in the editor's panels.</summary>
public sealed record EditorLabel(Point Position, string Text);

/// <summary>Flows buttons left to right and wraps to a new line at the right edge of an area; collects buttons and labels for one frame.</summary>
public sealed class FlowLayout
{
    private readonly Rectangle _area;
    private readonly UiUnit _scale;
    private readonly int _gap;
    private int _x, _y, _lineHeight;

    /// <summary>Starts at the top-left of <paramref name="area"/>.</summary>
    public FlowLayout(Rectangle area, UiUnit scale)
    {
        _area = area; _scale = scale; _gap = scale * 2;
        _x = area.X + _gap; _y = area.Y + _gap;
        _lineHeight = 9 * scale;
    }

    /// <summary>Buttons added so far.</summary>
    public List<EditorButton> Buttons { get; } = new();

    /// <summary>Labels added so far.</summary>
    public List<EditorLabel> Labels { get; } = new();

    /// <summary>The y below the last line.</summary>
    public int Bottom => _y + _lineHeight;

    /// <summary>Ends the current line.</summary>
    public void NewLine() { _x = _area.X + _gap; _y += _lineHeight + _gap; }

    /// <summary>Adds a caption on its own line.</summary>
    public void Label(string text)
    {
        if (_x > _area.X + _gap) NewLine();
        Labels.Add(new EditorLabel(new Point(_x, _y + _scale), text));
        _y += _lineHeight;
    }

    /// <summary>Adds a button after the previous one, wrapping when it would pass the area's right edge.</summary>
    public void Add(string text, bool active, Action click, bool enabled = true)
    {
        int w = BitmapFont.Measure(text, _scale) + _scale * 6;
        if (_x + w > _area.Right - _gap && _x > _area.X + _gap) NewLine();
        Buttons.Add(new EditorButton(new Rectangle(_x, _y, w, _lineHeight), text, active, click, enabled));
        _x += w + _gap;
    }
}
