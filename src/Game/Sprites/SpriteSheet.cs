// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rts.Game.Sprites;

/// <summary>
/// A sprite sheet described by a <c>&lt;id&gt;.sprite.toml</c> (see <see cref="SpriteManifest"/>). At load the PNG is
/// split once into two textures: BASE holds every non-house pixel, and MASK holds the house-paint pixels as
/// greyscale. <see cref="Draw"/> draws BASE untinted, then MASK tinted with the player's house colour, so the sheet
/// costs two textures whatever the number of players. Frames are row-major, and facing 0 is north, clockwise.
/// </summary>
/// <remarks>
/// Ownership: the sheet owns its GPU textures, so it is <see cref="IDisposable"/>. Dispose it when the game ends.
/// </remarks>
public sealed class SpriteSheet : IDisposable
{
    private readonly Texture2D _base;
    private readonly Texture2D? _mask;
    private bool _disposed;

    private SpriteSheet(SpriteManifest manifest, int columns, int rows, Texture2D baseTexture, Texture2D? mask)
    {
        Manifest = manifest;
        Columns = columns;
        Rows = rows;
        _base = baseTexture;
        _mask = mask;
    }

    /// <summary>The manifest this sheet was built from.</summary>
    public SpriteManifest Manifest { get; }

    /// <summary>Sprite id (the manifest's file stem).</summary>
    public string Id => Manifest.Id;

    /// <summary>Width of one frame in pixels.</summary>
    public int FrameW => Manifest.FrameW;

    /// <summary>Height of one frame in pixels.</summary>
    public int FrameH => Manifest.FrameH;

    /// <summary>Frames per sheet row.</summary>
    public int Columns { get; }

    /// <summary>Frame rows in the sheet.</summary>
    public int Rows { get; }

    /// <summary>Total number of frame cells in the sheet.</summary>
    public int FrameCount => Columns * Rows;

    /// <summary>Whether the sheet has house-paint pixels. A sheet without a house ramp draws only BASE.</summary>
    public bool HasHousePaint => _mask != null;

    /// <summary>The origin pixel in each frame. See <see cref="SpriteManifest"/> for what it means per kind of sprite.</summary>
    public Vector2 Origin => new(Manifest.OriginX, Manifest.OriginY);

    /// <summary>
    /// Loads a manifest and the PNG it names, then builds the BASE and MASK textures.
    /// </summary>
    /// <param name="device">Graphics device that owns the textures.</param>
    /// <param name="manifestPath">Full path to the <c>.sprite.toml</c> file.</param>
    /// <exception cref="InvalidDataException">The manifest or PNG does not match the layout it describes.</exception>
    public static SpriteSheet Load(GraphicsDevice device, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(device);
        SpriteManifest manifest = SpriteManifest.Load(manifestPath);

        string pngPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? ".", manifest.SheetFile);
        byte[] pixels;
        int width;
        int height;
        using (Texture2D source = ReadPng(device, pngPath))
        {
            width = source.Width;
            height = source.Height;
            pixels = new byte[width * height * 4];
            source.GetData(pixels);
        }

        if (width % manifest.FrameW != 0 || height % manifest.FrameH != 0)
        {
            throw new InvalidDataException(
                $"{pngPath}: {width}x{height} px is not a whole number of {manifest.FrameW}x{manifest.FrameH} frames");
        }
        int columns = width / manifest.FrameW;
        int rows = height / manifest.FrameH;
        int cells = columns * rows;
        foreach (SpriteState state in manifest.States)
        {
            if (state.First + state.CellCount > cells)
            {
                throw new InvalidDataException(
                    $"{manifest.SourcePath}: state '{state.Name}' needs cells {state.First}..{state.First + state.CellCount - 1}, "
                    + $"but {pngPath} has only {cells}");
            }
        }

        var basePixels = new byte[pixels.Length];
        byte[]? maskPixels = null;
        if (manifest.HouseRamp.Count == 0)
        {
            Array.Copy(pixels, basePixels, pixels.Length);
        }
        else
        {
            maskPixels = new byte[pixels.Length];
            HouseSplit.Split(pixels, manifest.HouseRamp.ToArray(), basePixels, maskPixels);
            HouseSplit.Premultiply(maskPixels);
        }
        HouseSplit.Premultiply(basePixels);

        var baseTexture = new Texture2D(device, width, height);
        baseTexture.SetData(basePixels);
        Texture2D? maskTexture = null;
        if (maskPixels != null)
        {
            maskTexture = new Texture2D(device, width, height);
            maskTexture.SetData(maskPixels);
        }

        return new SpriteSheet(manifest, columns, rows, baseTexture, maskTexture);
    }

    /// <summary>Finds a state by name.</summary>
    /// <exception cref="KeyNotFoundException">The sprite has no state with that name.</exception>
    public SpriteState State(string name) => Manifest.State(name);

    /// <summary>
    /// The sheet frame to show for a state, a facing and a simulation tick. See <see cref="SpriteManifest.FrameIndex"/>.
    /// </summary>
    public int FrameIndex(string state, byte facing, int tick) => Manifest.FrameIndex(state, facing, tick);

    /// <summary>Source rectangle of a frame, given its row-major index.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The frame index is outside the sheet.</exception>
    public Rectangle FrameRect(int frame)
    {
        if ((uint)frame >= (uint)FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        return new Rectangle((frame % Columns) * FrameW, (frame / Columns) * FrameH, FrameW, FrameH);
    }

    /// <summary>
    /// Draws a frame with its origin pixel on <paramref name="position"/>: BASE in white, then MASK tinted with
    /// <paramref name="house"/>. The caller must have begun the batch with the default alpha blend.
    /// </summary>
    /// <param name="batch">Batch in progress.</param>
    /// <param name="frame">Row-major frame index.</param>
    /// <param name="position">Screen position of the origin pixel.</param>
    /// <param name="house">House colour, for example <see cref="PlayerColours.Base"/>.</param>
    /// <param name="scale">Uniform scale (the camera zoom).</param>
    /// <param name="alpha">Opacity 0..1 (M3: stealthed and underground units). The textures are premultiplied, so this scales every channel.</param>
    public void Draw(SpriteBatch batch, int frame, Vector2 position, Color house, float scale, float alpha = 1f)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(batch);
        Rectangle rect = FrameRect(frame);
        batch.Draw(_base, position, rect, alpha >= 1f ? Color.White : Color.White * alpha, 0f, Origin, scale, SpriteEffects.None, 0f);
        if (_mask != null)
        {
            batch.Draw(_mask, position, rect, alpha >= 1f ? house : house * alpha, 0f, Origin, scale, SpriteEffects.None, 0f);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _base.Dispose();
        _mask?.Dispose();
    }

    private static Texture2D ReadPng(GraphicsDevice device, string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Texture2D.FromStream(device, stream);
    }
}
