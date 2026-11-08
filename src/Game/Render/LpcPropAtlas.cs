// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>
/// The data-backed prop atlas (S15 art-packs pilot): real prop sprites from <c>data/sprites/props/lpc_props.png</c> (Liberated Pixel Cup art, see
/// <c>docs/CREDITS.md</c>) mapped by <c>lpc_props.props.toml</c> onto existing <see cref="PropKind"/> values, recoloured lightly toward the theater's
/// land ramp. Every (kind, variant) the manifest does not cover keeps the <see cref="ProceduralPropAtlas"/> placeholder, so the atlas is always complete.
/// The texture is the placeholder atlas with the LPC sheet stacked underneath it. <c>RTS_PROPS=procedural</c> switches this off.
/// </summary>
public sealed class LpcPropAtlas : IPropAtlas
{
    /// <summary>How strongly (permille) an LPC pixel is pulled toward the theater's land ramp.</summary>
    public const int TintPermille = 320;

    private readonly ProceduralPropAtlas _fallback;
    private readonly Dictionary<(PropKind Kind, int Variant), PropRect> _frames = new();
    private readonly int[] _variants;

    private LpcPropAtlas(ProceduralPropAtlas fallback, byte[] rgba, int width, int height, Dictionary<(PropKind, int), PropRect> frames)
    {
        _fallback = fallback;
        Rgba = rgba;
        Width = width;
        Height = height;
        foreach (var kv in frames) _frames[kv.Key] = kv.Value;
        _variants = new int[Enum.GetValues<PropKind>().Length];
        for (int i = 0; i < _variants.Length; i++) _variants[i] = PropArt.Variants;
    }

    /// <summary>True when <c>RTS_PROPS</c> is <c>procedural</c>.</summary>
    public static bool ProceduralRequested =>
        string.Equals(Environment.GetEnvironmentVariable("RTS_PROPS"), "procedural", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public byte[] Rgba { get; }

    /// <summary>The kinds and variants that come from the data sheet (the rest are procedural).</summary>
    public IReadOnlyCollection<(PropKind Kind, int Variant)> DataFrames => _frames.Keys;

    /// <summary>The best atlas for a palette: the data-backed one when the props folder loads, else the placeholder atlas.</summary>
    /// <param name="palette">The theater palette.</param>
    /// <param name="propsDir">Folder holding <c>lpc_props.png</c> and <c>lpc_props.props.toml</c>; null means <c>data/sprites/props</c> beside the game.</param>
    public static IPropAtlas Create(TerrainPalette palette, string? propsDir = null)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var fallback = new ProceduralPropAtlas(palette);
        if (ProceduralRequested) return fallback;
        try
        {
            return Load(fallback, palette, propsDir ?? Path.Combine(AppContext.BaseDirectory, "data", "sprites", "props"));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or UnauthorizedAccessException or KeyNotFoundException or InvalidCastException)
        {
            return fallback;   // missing or broken art never stops the game
        }
    }

    /// <summary>Loads the data sheet over a placeholder atlas. Throws when the folder or files are missing or malformed.</summary>
    public static LpcPropAtlas Load(ProceduralPropAtlas fallback, TerrainPalette palette, string propsDir)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(palette);
        string manifest = Path.Combine(propsDir, "lpc_props.props.toml");
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(manifest)) ?? throw new InvalidDataException($"{manifest}: empty");
        (byte[] src, int sw, int sh) = PngRgba.Load(Path.Combine(propsDir, (string)root["file"]));

        int width = Math.Max(fallback.Width, sw), height = fallback.Height + sh;
        var rgba = new byte[width * height * 4];
        for (int y = 0; y < fallback.Height; y++) Buffer.BlockCopy(fallback.Rgba, y * fallback.Width * 4, rgba, y * width * 4, fallback.Width * 4);

        var frames = new Dictionary<(PropKind, int), PropRect>();
        foreach (object o in (TomlTableArray)root["frame"])
        {
            var t = (TomlTable)o;
            if (!Enum.TryParse((string)t["kind"], true, out PropKind kind)) throw new InvalidDataException($"{manifest}: unknown kind '{t["kind"]}'");
            int variant = (int)(long)t["variant"], x = (int)(long)t["x"], y = (int)(long)t["y"], w = (int)(long)t["w"], h = (int)(long)t["h"];
            if (x < 0 || y < 0 || x + w > sw || y + h > sh) throw new InvalidDataException($"{manifest}: {kind}/{variant} lies outside the sheet");
            TerrainPalette.LandShades? tint = TintFor(kind, palette);
            for (int yy = 0; yy < h; yy++)
                for (int xx = 0; xx < w; xx++)
                {
                    int s = ((y + yy) * sw + x + xx) * 4, d = ((fallback.Height + y + yy) * width + x + xx) * 4;
                    byte a = src[s + 3];
                    if (a == 0) continue;
                    TerrainPalette.Rgb c = new(src[s], src[s + 1], src[s + 2]);
                    if (tint is { } ramp) c = Tinted(c, ramp);
                    rgba[d] = (byte)(c.R * a / 255);          // premultiplied, as IPropAtlas promises
                    rgba[d + 1] = (byte)(c.G * a / 255);
                    rgba[d + 2] = (byte)(c.B * a / 255);
                    rgba[d + 3] = a;
                }

            frames[(kind, variant)] = new PropRect(x, fallback.Height + y, w, h, (int)(long)t["origin_x"], (int)(long)t["origin_y"]);
        }

        return new LpcPropAtlas(fallback, rgba, width, height, frames);
    }

    /// <summary>Rocks lean toward the Rock ramp and rubble toward the Rough ramp; trees and concrete keep their own colours.</summary>
    private static TerrainPalette.LandShades? TintFor(PropKind kind, TerrainPalette p) => kind switch
    {
        PropKind.Boulder => p.ShadesFor(LandType.Rock),
        PropKind.Rubble => p.ShadesFor(LandType.Rough),
        _ => null,
    };

    /// <summary>Maps the pixel's brightness onto dark..base..light of the ramp and mixes <see cref="TintPermille"/> of that into the original.</summary>
    public static TerrainPalette.Rgb Tinted(TerrainPalette.Rgb c, TerrainPalette.LandShades ramp)
    {
        int lum = (c.R * 2126 + c.G * 7152 + c.B * 722) / 10000;                      // 0..255
        TerrainPalette.Rgb target = lum < 128
            ? TerrainBake.Mix(ramp.Dark, ramp.Base, lum * 1000 / 128)
            : TerrainBake.Mix(ramp.Base, ramp.Light, (lum - 128) * 1000 / 127);
        return TerrainBake.Mix(c, target, TintPermille);
    }

    /// <inheritdoc />
    public int VariantCount(PropKind kind) => _variants[(int)kind];

    /// <inheritdoc />
    public PropRect Frame(PropKind kind, int variant)
    {
        int v = (variant % PropArt.Variants + PropArt.Variants) % PropArt.Variants;
        return _frames.TryGetValue((kind, v), out PropRect r) ? r : _fallback.Frame(kind, v);
    }
}
