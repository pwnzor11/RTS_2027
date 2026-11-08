// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Render;
using Rts.Game.Screens;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Ui.Llm;

/// <summary>
/// A modal one-line text box: type, Backspace, Ctrl+V to paste, Enter accepts, Esc cancels. A masked box shows only asterisks.
/// The accepted text goes to the callback; the screen keeps nothing after it closes.
/// </summary>
public sealed class TextEntryScreen : IScreen
{
    private static readonly Color PanelColour = new(0x16, 0x18, 0x14, 245);
    private static readonly Color BorderColour = new(0x6A, 0x72, 0x60);
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color TextColour = new(0xE2, 0xE4, 0xD8);
    private static readonly Color HintColour = new(0x9A, 0xA0, 0x90);
    private static readonly Color BoxFill = new(0x0C, 0x0E, 0x0A);

    private readonly GameServices _s;
    private readonly string _title;
    private readonly bool _masked;
    private readonly Action<string> _onAccept;
    private readonly TextEditBuffer _buffer;
    private double _clock;

    /// <summary>Creates the box. <paramref name="initial"/> is shown for editing (pass "" for a masked box).</summary>
    public TextEntryScreen(GameServices services, string title, string initial, bool masked, int maxLength, Action<string> onAccept)
    {
        _s = services;
        _title = title;
        _masked = masked;
        _onAccept = onAccept;
        _buffer = new TextEditBuffer(initial, maxLength);
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

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
        _clock = t.TotalGameTime.TotalSeconds;
        Apply(input);
    }

    /// <summary>Applies one frame of input. Separate from <see cref="Update"/> so tests can drive it.</summary>
    public void Apply(InputFrame input)
    {
        bool ctrl = input.Keyboard.IsKeyDown(Keys.LeftControl) || input.Keyboard.IsKeyDown(Keys.RightControl);
        if (ctrl && input.ConsumePressed(Keys.V)) _buffer.Append(ClipboardText.Read());
        foreach (char c in input.ConsumeText()) _buffer.Append(c.ToString());
        if (input.ConsumePressed(Keys.Back)) _buffer.Backspace();
        if (input.ConsumePressed(Keys.Escape))
        {
            _s.Screens.Pop();
            return;
        }

        if (input.ConsumePressed(Keys.Enter))
        {
            string text = _buffer.Text.Trim();
            _s.Screens.Pop();
            _onAccept(text);
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        var scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        int pad = 4 * scale;
        int line = BitmapFont.LineHeight(scale, FontWeight.Regular);
        int width = Math.Min(vp.Width - 2 * pad, 70 * BitmapFont.Advance * scale / 2 + 2 * pad);
        int height = pad * 2 + line * 4 + pad;
        var panel = new Rectangle((vp.Width - width) / 2, (vp.Height - height) / 2, width, height);
        Shapes.Fill(sb, _s.Pixel, panel, PanelColour);
        Shapes.Outline(sb, _s.Pixel, panel, BorderColour, Math.Max(1, scale / 2));
        BitmapFont.Draw(sb, _s.Pixel, _title, new Vector2(panel.X + pad, panel.Y + pad), TitleColour, scale);

        var box = new Rectangle(panel.X + pad, panel.Y + pad + line + pad / 2, panel.Width - 2 * pad, line + pad);
        Shapes.Fill(sb, _s.Pixel, box, BoxFill);
        Shapes.Outline(sb, _s.Pixel, box, BorderColour);
        int maxChars = Math.Max(1, (box.Width - 2 * scale) / (BitmapFont.Advance * scale / 2) - 1);
        string shown = _masked ? _buffer.Masked() : _buffer.Text;
        if (shown.Length > maxChars) shown = shown[^maxChars..];
        bool caret = (int)(_clock * 2) % 2 == 0;
        BitmapFont.Draw(sb, _s.Pixel, shown + (caret ? "_" : " "), new Vector2(box.X + scale * 2, box.Y + pad / 2), TextColour, scale);
        BitmapFont.Draw(sb, _s.Pixel, LlmUiText.EntryHint, new Vector2(panel.X + pad, box.Bottom + pad / 2), HintColour, scale);
    }
}
