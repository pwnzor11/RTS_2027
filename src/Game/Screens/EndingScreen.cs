// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Ui.Fonts;
using Rts.Game.Ui;

namespace Rts.Game.Screens;

/// <summary>
/// The Fallout-style ending slides of a finished campaign (plan 3.8, M8-16): one still and caption at a time. Click, Enter or Space shows the
/// next slide, Esc skips the rest. A slide whose image file (<c>&lt;imagesDir&gt;/&lt;image id&gt;.png</c>) is missing shows a plain dark panel with the caption.
/// </summary>
public sealed class EndingScreen : IScreen
{
    private const int WrapChars = 70;
    private readonly GameServices _s;
    private readonly EndingModel _model;
    private readonly string _imagesDir;
    private readonly Dictionary<string, Texture2D?> _images = new();

    /// <summary>Creates the screen.</summary>
    /// <param name="services">Shared services.</param>
    /// <param name="model">The slides.</param>
    /// <param name="imagesDir">Folder with the slide images.</param>
    public EndingScreen(GameServices services, EndingModel model, string imagesDir)
    {
        _s = services;
        _model = model;
        _imagesDir = imagesDir;
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
        foreach (Texture2D? tex in _images.Values) tex?.Dispose();
        _images.Clear();
    }

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        if (_model.Finished || _model.Current == null) { _s.Screens.Pop(); return; }
        if (input.ConsumePressed(Keys.Escape)) _model.Skip();
        else if (input.ConsumePressed(Keys.Enter) || input.ConsumePressed(Keys.Space) || input.LeftPressed)
        {
            input.ConsumeLeft();
            _model.Next();
        }

        if (_model.Finished) _s.Screens.Pop();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        if (_model.Current is not { } frame) return;
        var scale = _s.UiScale;
        Viewport vp = _s.Viewport;
        int lineHeight = BitmapFont.LineHeight(scale, FontWeight.Regular) + 4;
        int imageH = vp.Height * 55 / 100, imageW = Math.Min(vp.Width - 32, imageH * 16 / 9);
        var area = new Rectangle((vp.Width - imageW) / 2, 24, imageW, imageH);
        if (ImageOf(frame.Image) is { } tex) sb.Draw(tex, area, Color.White);
        else sb.Draw(_s.Pixel, area, new Color(0x0C, 0x0E, 0x0A));
        int y = area.Bottom + lineHeight;
        foreach (string line in MissionOverlay.Wrap(frame.Caption, WrapChars))
        {
            BitmapFont.Draw(sb, _s.Pixel, line, new Vector2(Math.Max(16, (vp.Width - BitmapFont.Measure(line, scale)) / 2), y), new Color(0xE2, 0xE4, 0xD8), scale);
            y += lineHeight;
        }

        string hint = $"{_model.Index + 1}/{_model.Frames.Count}   Click or Enter: next   Esc: skip";
        BitmapFont.Draw(sb, _s.Pixel, hint, new Vector2(16, vp.Height - lineHeight - 8), new Color(0x8A, 0x92, 0x80), scale);
    }

    // A missing or unreadable image is remembered as null so the dark panel is used without retrying every frame.
    private Texture2D? ImageOf(string id)
    {
        if (_images.TryGetValue(id, out Texture2D? cached)) return cached;
        Texture2D? tex = null;
        try
        {
            string path = Path.Combine(_imagesDir, id + ".png");
            if (File.Exists(path))
            {
                using FileStream fs = File.OpenRead(path);
                tex = Texture2D.FromStream(_s.Graphics, fs);
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            tex = null;
        }

        _images[id] = tex;
        return tex;
    }
}
