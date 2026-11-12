// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Ui;
using Rts.Game.Ui.Fonts;

namespace Rts.Game.Screens;

/// <summary>
/// The credits roll (M10-20): the credits scroll up the window and the screen closes when they have gone by. Esc, Enter or a click skips it; Space pauses.
/// Reached from Settings and after the last mission of a campaign.
/// </summary>
public sealed class CreditsScreen : IScreen
{
    private static readonly Color TitleColour = new(0xD8, 0xC8, 0x8A);
    private static readonly Color HeadingColour = new(0xE8, 0xA8, 0x28);
    private static readonly Color NameColour = new(0xEE, 0xEE, 0xDE);
    private static readonly Color TextColour = new(0xB8, 0xBC, 0xAA);

    private readonly GameServices _s;
    private readonly CreditsDoc _doc;
    private readonly CreditsRoll _roll = new();
    private IReadOnlyList<CreditsLine> _lines = Array.Empty<CreditsLine>();
    private int _wrappedFor = -1;
    private bool _paused;
    private int _lineHeight = 10, _viewHeight = 100;

    /// <summary>Creates the screen from the credits file next to the rules (or the built-in fallback).</summary>
    public CreditsScreen(GameServices services) : this(services, CreditsDoc.Load(services.RulesDir))
    {
    }

    /// <summary>Creates the screen over given credits (tests).</summary>
    public CreditsScreen(GameServices services, CreditsDoc doc)
    {
        _s = services;
        _doc = doc;
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
        // TODO(M10-E merge): ask the music player for the "credits" state here once MusicState.Credits exists (and go back to Menu in Exit).
    }

    /// <inheritdoc />
    public void Exit()
    {
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        if (input.ConsumePressed(Keys.Escape) || input.ConsumePressed(Keys.Enter) || input.LeftPressed)
        {
            input.ConsumeLeft();
            _s.Screens.Pop();
            return;
        }

        if (input.ConsumePressed(Keys.Space)) _paused = !_paused;
        if (_paused) return;
        _roll.Advance();
        if (_lines.Count > 0 && _roll.IsFinished(_lines.Count, _lineHeight, _viewHeight)) _s.Screens.Pop();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        UiUnit scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        _lineHeight = BitmapFont.LineHeight(scale, FontWeight.Regular) + 2 * scale;
        _viewHeight = vp.Height;
        int width = Math.Min(vp.Width - 8 * scale, 90 * scale * BitmapFont.Advance / 2);
        if (_wrappedFor != width)
        {
            _wrappedFor = width;
            _lines = _doc.Lines(width, text => BitmapFont.Measure(text, scale));
        }

        for (int i = 0; i < _lines.Count; i++)
        {
            int y = _roll.LineY(i, _lineHeight, _viewHeight);
            if (y < -_lineHeight || y > vp.Height) continue;
            CreditsLine line = _lines[i];
            if (line.Kind == CreditsLineKind.Blank) continue;
            (Color colour, FontWeight weight) = line.Kind switch
            {
                CreditsLineKind.Title => (TitleColour, FontWeight.Bold),
                CreditsLineKind.Heading => (HeadingColour, FontWeight.Bold),
                CreditsLineKind.Name => (NameColour, FontWeight.Bold),
                _ => (TextColour, FontWeight.Regular),
            };
            int w = BitmapFont.Measure(line.Text, scale, weight);
            BitmapFont.Draw(sb, _s.Pixel, line.Text, new Vector2((vp.Width - w) / 2, y), colour, scale, weight);
        }

        string hint = _paused ? "Paused    Space to go on    Esc to leave" : "Space to pause    Esc to leave";
        BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(4 * scale, vp.Height - _lineHeight), new Color(0x70, 0x74, 0x68), scale);
    }
}
