// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// The one place that lists the prop art sources, in priority order. <see cref="DecorLayer"/> asks <see cref="Create"/> and never names a source.
/// To add a source, insert it before the procedural entry (a source that does not cover every kind implements <see cref="IPropSource"/>).
/// </summary>
public static class PropAtlases
{
    /// <summary>Set to 1 to skip every data-backed source and draw the procedural placeholders (for before/after screenshots).</summary>
    public const string ProceduralOnlyVariable = "RTS_PROCEDURAL_PROPS";

    /// <summary>Builds the prop atlas for a theater: 0 A.D.-derived renders where they exist, the procedural art for the rest.</summary>
    /// <param name="palette">The theater palette (colours the procedural fallback).</param>
    /// <param name="theater">The theater id (<see cref="TerrainPalette.TheaterIdFor"/>), which picks the graded 0 A.D. atlas.</param>
    public static IPropAtlas Create(TerrainPalette palette, string theater)
    {
        ArgumentNullException.ThrowIfNull(palette);
        var sources = new List<IPropAtlas>();
        if (Environment.GetEnvironmentVariable(ProceduralOnlyVariable) != "1")
        {
            ZeroAdPropAtlas? zeroAd = ZeroAdPropAtlas.TryLoad(theater);
            if (zeroAd is not null) sources.Add(zeroAd);
        }

        sources.Add(new ProceduralPropAtlas(palette));
        return sources.Count == 1 ? sources[0] : new LayeredPropAtlas(sources);
    }
}
