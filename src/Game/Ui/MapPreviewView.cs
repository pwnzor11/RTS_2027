// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Render;

namespace Rts.Game.Ui;

/// <summary>Draws a <see cref="MapPreviewImage"/> as a framed texture and keeps the texture until the picture changes (M10-18). Dispose it when the screen leaves.</summary>
public sealed class MapPreviewView : IDisposable
{
    private Texture2D? _texture;
    private MapPreviewImage? _shown;

    /// <summary>Draws <paramref name="image"/> in a square-fitting box inside <paramref name="area"/>; null draws an empty frame with "No preview".</summary>
    public void Draw(SpriteBatch sb, Texture2D pixel, Rectangle area, MapPreviewImage? image, UiUnit scale)
    {
        Shapes.Fill(sb, pixel, area, new Color(0x0A, 0x0C, 0x08));
        if (image is null)
        {
            const string none = "No preview";
            BitmapFont.Draw(sb, pixel, none, new Vector2(area.Center.X - BitmapFont.Measure(none, scale) / 2, area.Center.Y), new Color(0x90, 0x94, 0x86), scale);
        }
        else
        {
            if (!ReferenceEquals(_shown, image))
            {
                _texture?.Dispose();
                _texture = new Texture2D(pixel.GraphicsDevice, image.Width, image.Height);
                var data = new Color[image.Width * image.Height];
                for (int i = 0; i < data.Length; i++)
                    data[i] = new Color(image.Rgba[i * 4], image.Rgba[i * 4 + 1], image.Rgba[i * 4 + 2], image.Rgba[i * 4 + 3]);
                _texture.SetData(data);
                _shown = image;
            }

            Rectangle fit = Fit(area, image.Width, image.Height);
            sb.Draw(_texture!, fit, Color.White);
        }

        Shapes.Outline(sb, pixel, area, new Color(0x6A, 0x72, 0x60));
    }

    /// <summary>The largest rectangle with the picture's shape centred in <paramref name="area"/>.</summary>
    public static Rectangle Fit(Rectangle area, int width, int height)
    {
        if (width <= 0 || height <= 0 || area.IsEmpty) return Rectangle.Empty;
        long w = area.Width, h = (long)area.Width * height / width;
        if (h > area.Height) { h = area.Height; w = (long)area.Height * width / height; }
        return new Rectangle(area.X + (area.Width - (int)w) / 2, area.Y + (area.Height - (int)h) / 2, (int)w, (int)h);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
        _shown = null;
    }
}
