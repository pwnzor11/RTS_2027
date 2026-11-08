// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Graphics;
using Rts.Data;

namespace Rts.Game.Ui.Fonts;

/// <summary>
/// Finds and caches the UI font atlases in <c>data/fonts</c>, beside <c>data/rules</c>. A face that fails to load is
/// remembered as failed, so the caller falls back to the embedded 5x7 font without retrying every frame.
/// </summary>
public static class FontLibrary
{
    /// <summary>Pixel sizes the build script produces by default (tools/fonts/build_atlas.py).</summary>
    public static IReadOnlyList<int> Sizes { get; } = new[] { 9, 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 40, 48, 56, 64 };

    private static readonly Dictionary<(FontWeight Weight, int Size), FontAtlas?> Metrics = new();
    private static readonly HashSet<(FontWeight Weight, int Size)> TextureFailed = new();
    private static string? _directoryOverride;

    /// <summary>The reason the most recent load failed, or null.</summary>
    public static string? LastError { get; private set; }

    /// <summary>
    /// The folder the atlases are read from: the override when one is set, otherwise <c>data/fonts</c> beside the
    /// nearest <c>data/rules</c> above the executable. Null when neither is found.
    /// </summary>
    public static string? Directory
    {
        get
        {
            if (_directoryOverride is not null) return _directoryOverride;
            string? rulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
            return rulesDir is null ? null : Path.Combine(Path.GetDirectoryName(rulesDir)!, "fonts");
        }
    }

    /// <summary>
    /// Points the library at another folder and clears the cache. Pass null to return to the default. Used by tests and
    /// tools; a missing folder makes every lookup fail, which exercises the 5x7 fallback.
    /// </summary>
    /// <param name="directory">The folder to read, or null for the default.</param>
    public static void UseDirectory(string? directory)
    {
        _directoryOverride = directory;
        Metrics.Clear();
        TextureFailed.Clear();
        LastError = null;
    }

    /// <summary>The face file-name stem for a weight.</summary>
    /// <param name="weight">The weight.</param>
    public static string FaceName(FontWeight weight) => weight switch
    {
        FontWeight.Bold => "noto-sans-bold",
        _ => "noto-sans-regular",
    };

    /// <summary>
    /// The metrics of one face and size, or null when the folder or file is missing. Needs no graphics device, so
    /// layout code can measure text before anything is drawn.
    /// </summary>
    /// <param name="weight">The weight.</param>
    /// <param name="size">A size from <see cref="Sizes"/>.</param>
    public static FontAtlas? Get(FontWeight weight, int size)
    {
        if (Metrics.TryGetValue((weight, size), out FontAtlas? cached)) return cached;
        FontAtlas? atlas = null;
        string? dir = Directory;
        if (dir is null)
        {
            LastError = "data/fonts not found";
        }
        else if (!FontAtlas.TryLoad(dir, FaceName(weight), size, out atlas, out string? error))
        {
            LastError = error;
        }
        Metrics[(weight, size)] = atlas;
        return atlas;
    }

    /// <summary>
    /// The atlas with its texture loaded on <paramref name="device"/>, or null when the metrics or the PNG cannot be
    /// loaded. A failed texture is not retried.
    /// </summary>
    /// <param name="device">The graphics device the texture is created on.</param>
    /// <param name="weight">The weight.</param>
    /// <param name="size">A size from <see cref="Sizes"/>.</param>
    public static FontAtlas? GetWithTexture(GraphicsDevice device, FontWeight weight, int size)
    {
        FontAtlas? atlas = Get(weight, size);
        if (atlas is null || TextureFailed.Contains((weight, size))) return null;
        if (atlas.Texture is not null) return atlas;
        if (atlas.EnsureTexture(device, Directory!, out string? error)) return atlas;
        LastError = error;
        TextureFailed.Add((weight, size));
        return null;
    }

    /// <summary>The available size nearest to <paramref name="pixels"/>; on a tie, the smaller size.</summary>
    /// <param name="pixels">Wanted em size in pixels.</param>
    public static int NearestSize(float pixels)
    {
        int best = Sizes[0];
        foreach (int size in Sizes)
        {
            if (MathF.Abs(size - pixels) < MathF.Abs(best - pixels)) best = size;
        }
        return best;
    }
}
