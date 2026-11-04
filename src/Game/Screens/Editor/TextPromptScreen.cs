// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;

namespace Rts.Game.Screens.Editor;

/// <summary>A small modal that asks for one line of text (a map name). Enter or OK accepts, Esc or Cancel closes.</summary>
public sealed class TextPromptScreen : IScreen
{
    private const int MaxLength = 40;

    private readonly GameServices _s;
    private readonly Action<string> _accept;
    private readonly MenuPanel _panel;
    private string _text;
    private Point _mouse;

    /// <summary>Creates the prompt. <paramref name="accept"/> runs with the trimmed text (never empty) after the prompt has closed.</summary>
    public TextPromptScreen(GameServices services, string title, string initial, Action<string> accept)
    {
        _s = services;
        _accept = accept;
        _text = initial;
        _panel = new MenuPanel(title, new[]
        {
            new MenuItem(() => _text + "_"),
            new MenuItem(() => "OK", Accept),
            new MenuItem(() => "Cancel", Close),
        }) { Selected = 1 };
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => false;

    /// <inheritdoc />
    public void Enter() { }

    /// <inheritdoc />
    public void Exit() { }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape)) { Close(); return; }
        foreach (char c in input.ConsumeText()) _text = Typed(_text, c);
        if (input.ConsumePressed(Keys.Back) && _text.Length > 0) _text = _text[..^1];
        _panel.Update(input, _s.Viewport, _s.UiScale);
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <summary>The text after typing <paramref name="c"/>: printable ASCII is appended up to 40 characters, anything else is ignored.</summary>
    public static string Typed(string text, char c) => c >= ' ' && c < 127 && text.Length < MaxLength ? text + c : text;

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private void Accept()
    {
        string v = _text.Trim();
        if (v.Length == 0) return;
        _s.Screens.Pop();
        _accept(v);
    }

    private void Close() => _s.Screens.Pop();
}
