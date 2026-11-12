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

namespace Rts.Game.Profile;

/// <summary>M10-19: the Profile page reached from the main menu: campaign medals and skirmish records. Up / Down or the wheel scroll; Esc goes back.</summary>
public sealed class ProfileScreen : IScreen
{
    private readonly GameServices _s;
    private readonly IReadOnlyList<string> _lines;
    private Rectangle _backRect;
    private Point _mouse;
    private int _scroll;

    /// <summary>Creates the page from the profile file for these services.</summary>
    public ProfileScreen(GameServices services)
    {
        _s = services;
        _lines = ProfileModel.Lines(PlayerProfile.Load(ProfileRecorder.PathFor(services)));
    }

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
        if (input.ConsumePressed(Keys.Down)) _scroll++;
        if (input.ConsumePressed(Keys.Up)) _scroll--;
        if (input.ConsumePressed(Keys.PageDown)) _scroll += 10;
        if (input.ConsumePressed(Keys.PageUp)) _scroll -= 10;
        if (input.WheelDelta != 0) _scroll -= Math.Sign(input.WheelDelta) * 3;
        if (input.LeftPressed && _backRect.Contains(_mouse)) { input.ConsumeMouse(); _s.Screens.Pop(); return; }
        input.ConsumeKeyboard();
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        UiUnit u = _s.UiScale;
        const int margin = 16;
        int lh = BitmapFont.LineHeight(u, FontWeight.Regular) + 4;
        BitmapFont.Draw(sb, _s.Pixel, "Profile", new Vector2(margin, margin), Color.White, u, FontWeight.Bold);
        _backRect = new Rectangle(margin, _s.Viewport.Height - margin - lh - 4, BitmapFont.Measure("Back", u) + 24, lh + 4);
        Shapes.Fill(sb, _s.Pixel, _backRect, _backRect.Contains(_mouse) ? new Color(70, 90, 120) : new Color(40, 50, 66));
        BitmapFont.Draw(sb, _s.Pixel, "Back", new Vector2(_backRect.X + 12, _backRect.Y + 2), Color.White, u);

        int top = margin + 2 * lh;
        int visible = Math.Max(1, (_backRect.Y - 8 - top) / lh);
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, _lines.Count - visible));
        for (int i = 0; i < visible && _scroll + i < _lines.Count; i++)
        {
            string line = _lines[_scroll + i];
            bool heading = line.Length > 0 && line[0] != ' ';
            BitmapFont.Draw(sb, _s.Pixel, line, new Vector2(margin, top + i * lh), heading ? Color.White : new Color(200, 205, 215), u,
                heading ? FontWeight.Bold : FontWeight.Regular);
        }
    }
}
