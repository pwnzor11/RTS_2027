// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>A small modal message: a title, wrapped text lines and an OK row. Enter, Esc or a click on OK closes it.</summary>
public sealed class NoticeScreen : IScreen
{
    private const int WrapChars = 52;

    private readonly GameServices _s;
    private readonly MenuPanel _panel;
    private Point _mouse;

    /// <summary>Creates the notice. <paramref name="text"/> is wrapped to a fixed width; newlines are kept.</summary>
    public NoticeScreen(GameServices services, string title, string text)
    {
        _s = services;
        var items = new List<MenuItem>();
        foreach (string line in TextWrap.Wrap(text, WrapChars * BitmapFont.Advance, l => l.Length * BitmapFont.Advance))
        {
            string shown = line;
            items.Add(new MenuItem(() => shown));
        }
        items.Add(new MenuItem(() => "OK", Close));
        _panel = new MenuPanel(title, items) { Selected = items.Count - 1 };
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
        _mouse = input.MousePosition;
        if (input.ConsumePressed(Keys.Escape))
        {
            Close();
            return;
        }

        _panel.Update(input, _s.Viewport, _s.UiScale);
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillDim(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private void Close()
    {
        MenuSounds.Play(_s.Audio, MenuSounds.Back);
        _s.Screens.Pop();
    }
}
