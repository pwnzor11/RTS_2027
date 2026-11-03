using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Sprites;

/// <summary>
/// A sprite sheet described by a .toml file next to its PNG (format: art/pilot/sprite_meta_example.toml).
/// At load it makes one house-recoloured copy of the whole sheet per player, so drawing never recolours.
/// Frames are row-major; frame 0 faces north and facings run clockwise.
/// </summary>
public sealed class SpriteSheet
{
    private readonly Texture2D[] _textures;

    private SpriteSheet(Texture2D[] textures, int framePx, int columns, int rows, int facings, Vector2 anchor)
    {
        _textures = textures;
        FramePx = framePx;
        Columns = columns;
        Rows = rows;
        Facings = facings;
        Anchor = anchor;
    }

    /// <summary>Width and height of one square frame, in pixels.</summary>
    public int FramePx { get; }

    /// <summary>Frames per row.</summary>
    public int Columns { get; }

    /// <summary>Frame rows.</summary>
    public int Rows { get; }

    /// <summary>Number of facings in the sheet. Equals the number of frames.</summary>
    public int Facings { get; }

    /// <summary>Ground origin inside each frame (the cell centre). Used as the draw origin.</summary>
    public Vector2 Anchor { get; }

    /// <summary>Loads the .toml description and the PNG it names, then builds the per-player copies.</summary>
    /// <param name="device">Graphics device that owns the textures.</param>
    /// <param name="metaPath">Full path to the .toml file.</param>
    /// <exception cref="InvalidDataException">The description or PNG does not match the expected layout.</exception>
    public static SpriteSheet Load(GraphicsDevice device, string metaPath)
    {
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(metaPath))
            ?? throw new InvalidDataException($"{metaPath}: empty sprite description");

        TomlTable sheet = Table(root, "sheet", metaPath);
        string file = Text(sheet, "file", metaPath);
        int framePx = Int(sheet, "frame_px", metaPath);
        int columns = Int(sheet, "columns", metaPath);
        int rows = Int(sheet, "rows", metaPath);
        int facings = Int(sheet, "facings", metaPath);
        if (facings != columns * rows || facings is not (8 or 16 or 32))
        {
            throw new InvalidDataException($"{metaPath}: facings must be 8, 16 or 32 and equal columns * rows");
        }

        TomlTable anchorTable = Table(root, "anchor", metaPath);
        var anchor = new Vector2(Int(anchorTable, "x", metaPath), Int(anchorTable, "y", metaPath));

        uint[] houseRamp = ReadRamp(Table(root, "house", metaPath), metaPath);
        if (houseRamp.Length != PlayerColours.RampSteps)
        {
            throw new InvalidDataException($"{metaPath}: house_ramp must have {PlayerColours.RampSteps} colours");
        }

        string pngPath = Path.Combine(Path.GetDirectoryName(metaPath) ?? ".", file);
        byte[] pixels;
        int width;
        int height;
        using (Texture2D source = ReadPng(device, pngPath))
        {
            width = source.Width;
            height = source.Height;
            if (width != columns * framePx || height != rows * framePx)
            {
                throw new InvalidDataException($"{pngPath}: expected {columns * framePx}x{rows * framePx} px");
            }

            pixels = new byte[width * height * 4];
            source.GetData(pixels);
        }

        var textures = new Texture2D[PlayerColours.Count];
        for (int player = 0; player < textures.Length; player++)
        {
            byte[] copy = (byte[])pixels.Clone();
            HouseRemap.Remap(copy, houseRamp, PlayerColours.Ramp(player));
            HouseRemap.Premultiply(copy);

            var texture = new Texture2D(device, width, height);
            texture.SetData(copy);
            textures[player] = texture;
        }

        return new SpriteSheet(textures, framePx, columns, rows, facings, anchor);
    }

    /// <summary>The recoloured sheet for a player. Players beyond the defined colours wrap around.</summary>
    public Texture2D Texture(int player) => _textures[player % _textures.Length];

    /// <summary>Source rectangle of a frame, given its row-major index.</summary>
    public Rectangle FrameRect(int frame) =>
        new((frame % Columns) * FramePx, (frame / Columns) * FramePx, FramePx, FramePx);

    private static Texture2D ReadPng(GraphicsDevice device, string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Texture2D.FromStream(device, stream);
    }

    private static uint[] ReadRamp(TomlTable house, string metaPath)
    {
        if (!house.TryGetValue("house_ramp", out object? raw) || raw is not TomlArray array)
        {
            throw new InvalidDataException($"{metaPath}: [house] needs house_ramp as an array of #RRGGBB strings");
        }

        var ramp = new uint[array.Count];
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not string hex || hex.Length != 7 || hex[0] != '#'
                || !uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
            {
                throw new InvalidDataException($"{metaPath}: house_ramp entries must be #RRGGBB");
            }
            ramp[i] = value;
        }
        return ramp;
    }

    private static TomlTable Table(TomlTable parent, string key, string metaPath) =>
        parent.TryGetValue(key, out object? raw) && raw is TomlTable table
            ? table
            : throw new InvalidDataException($"{metaPath}: missing [{key}] table");

    private static string Text(TomlTable table, string key, string metaPath) =>
        table.TryGetValue(key, out object? raw) && raw is string text
            ? text
            : throw new InvalidDataException($"{metaPath}: '{key}' must be a string");

    private static int Int(TomlTable table, string key, string metaPath)
    {
        if (table.TryGetValue(key, out object? raw) && raw is long value && value >= 0 && value <= int.MaxValue)
        {
            return (int)value;
        }
        throw new InvalidDataException($"{metaPath}: '{key}' must be a non-negative integer");
    }
}
