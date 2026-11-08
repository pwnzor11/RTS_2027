// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// Several prop atlases in priority order merged into one texture: for each <see cref="PropKind"/> the first source that covers it supplies every
/// variant. Frames keep their own size and origin. Sources are read only through <see cref="IPropAtlas"/> / <see cref="IPropSource"/>, so a
/// new art source is one more entry in the list (see <see cref="PropAtlases"/>); the last source should cover everything (the procedural atlas).
/// </summary>
public sealed class LayeredPropAtlas : IPropAtlas
{
    private static readonly PropKind[] Kinds = Enum.GetValues<PropKind>();
    private readonly PropRect[][] _frames;
    private readonly int[] _sourceIndex;

    /// <summary>Builds the merged atlas. At least one source must cover every kind.</summary>
    /// <param name="sources">Atlases, highest priority first.</param>
    public LayeredPropAtlas(IReadOnlyList<IPropAtlas> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sourceIndex = new int[Kinds.Length];
        _frames = new PropRect[Kinds.Length][];
        int cellW = 1, cellH = 1, maxVariants = 1;
        for (int k = 0; k < Kinds.Length; k++)
        {
            int s = Pick(sources, Kinds[k]);
            _sourceIndex[k] = s;
            int n = sources[s].VariantCount(Kinds[k]);
            maxVariants = Math.Max(maxVariants, n);
            for (int v = 0; v < n; v++)
            {
                PropRect r = sources[s].Frame(Kinds[k], v);
                cellW = Math.Max(cellW, r.Width);
                cellH = Math.Max(cellH, r.Height);
            }
        }

        Width = cellW * maxVariants;
        Height = cellH * Kinds.Length;
        Rgba = new byte[Width * Height * 4];
        for (int k = 0; k < Kinds.Length; k++)
        {
            IPropAtlas src = sources[_sourceIndex[k]];
            int n = src.VariantCount(Kinds[k]);
            _frames[k] = new PropRect[n];
            for (int v = 0; v < n; v++)
            {
                PropRect r = src.Frame(Kinds[k], v);
                var dst = new PropRect(v * cellW, k * cellH, r.Width, r.Height, r.OriginX, r.OriginY);
                _frames[k][v] = dst;
                for (int y = 0; y < r.Height; y++)
                    Buffer.BlockCopy(src.Rgba, ((r.Y + y) * src.Width + r.X) * 4, Rgba, ((dst.Y + y) * Width + dst.X) * 4, r.Width * 4);
            }
        }
    }

    /// <inheritdoc />
    public int Width { get; }

    /// <inheritdoc />
    public int Height { get; }

    /// <inheritdoc />
    public byte[] Rgba { get; }

    /// <summary>Index (in the source list) of the source that draws <paramref name="kind"/>.</summary>
    public int SourceOf(PropKind kind) => _sourceIndex[(int)kind];

    /// <inheritdoc />
    public int VariantCount(PropKind kind) => _frames[(int)kind].Length;

    /// <inheritdoc />
    public PropRect Frame(PropKind kind, int variant)
    {
        PropRect[] f = _frames[(int)kind];
        return f[(variant % f.Length + f.Length) % f.Length];
    }

    private static int Pick(IReadOnlyList<IPropAtlas> sources, PropKind kind)
    {
        for (int i = 0; i < sources.Count; i++)
            if (sources[i] is not IPropSource ps || ps.Covers(kind)) return i;
        throw new InvalidOperationException($"no prop source covers {kind}");
    }
}
