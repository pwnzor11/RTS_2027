// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data.MapGen;

/// <summary>
/// Paint surface of the procedural generator (design 08 §1). Every write goes to all images of a cell under the grid's symmetry
/// (or to none), so the result is exactly symmetric by construction. Scans run in cell-index order.
/// </summary>
internal sealed class GenGrid
{
    public readonly int N;
    public readonly int K;
    public readonly MapSymmetry Symmetry;
    public readonly LandType Base;
    public readonly LandType[] Land;
    public readonly byte[] Height;
    public readonly OverlayKind[] Overlay;
    public readonly byte[] Density;
    /// <summary>Start surroundings: terrain features keep out.</summary>
    public readonly bool[] Reserved;
    /// <summary>Start cores (Chebyshev 3): resources keep out too.</summary>
    public readonly bool[] Core;
    public readonly bool[] Ramp;
    public readonly bool[] River;
    public readonly bool[] Feature;
    private readonly int[] _orbit;

    public GenGrid(int n, MapSymmetry symmetry, LandType baseLand)
    {
        N = n; Symmetry = symmetry; Base = baseLand; K = MapSymmetries.Order(symmetry);
        int c = n * n;
        Land = new LandType[c]; Height = new byte[c]; Overlay = new OverlayKind[c]; Density = new byte[c];
        Reserved = new bool[c]; Core = new bool[c]; Ramp = new bool[c]; River = new bool[c]; Feature = new bool[c];
        Array.Fill(Land, baseLand);
        _orbit = new int[c * K];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var img = MapSymmetries.Images(symmetry, n, n, x, y);
                int i = y * n + x;
                for (int k = 0; k < K; k++) _orbit[i * K + k] = img[k].Y * n + img[k].X;
            }
    }

    public bool In(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
    public int Idx(int x, int y) => y * N + x;
    public ReadOnlySpan<int> Images(int i) => _orbit.AsSpan(i * K, K);

    /// <summary>Untouched flat base ground, outside the start surroundings, rivers, ramp approaches and features.</summary>
    public bool Free(int i) => Land[i] == Base && Overlay[i] == OverlayKind.None && !Reserved[i] && Height[i] == 0 && !River[i] && !Feature[i] && !Ramp[i];

    public bool All(int i, Func<int, bool> pred)
    {
        foreach (int j in Images(i)) if (!pred(j)) return false;
        return true;
    }

    public bool AllFree(int i) => All(i, Free);

    /// <summary>Sets land on every image (clearing resources). Returns the number of cells written.</summary>
    public int SetLand(int i, LandType land)
    {
        int c = 0;
        foreach (int j in Images(i))
        {
            if (Land[j] != land) c++;
            Land[j] = land; Overlay[j] = OverlayKind.None; Density[j] = 0;
        }
        return c;
    }

    /// <summary>Paints <paramref name="land"/> on every image when all images are <see cref="Free"/>.</summary>
    public int PaintFree(int x, int y, LandType land) => In(x, y) && AllFree(Idx(x, y)) ? SetLand(Idx(x, y), land) : 0;

    public int SetResource(int i, LandType land, OverlayKind overlay, byte density)
    {
        int c = 0;
        foreach (int j in Images(i))
        {
            if (Overlay[j] != overlay) c++;
            Land[j] = land; Overlay[j] = overlay; Density[j] = density;
        }
        return c;
    }

    public void SetHeight(int i, byte h) { foreach (int j in Images(i)) Height[j] = h; }
    public void MarkReserved(int i) { foreach (int j in Images(i)) Reserved[j] = true; }
    public void MarkCore(int i) { foreach (int j in Images(i)) Core[j] = true; }
    public void MarkRamp(int i) { foreach (int j in Images(i)) Ramp[j] = true; }
    public void MarkRiver(int i) { foreach (int j in Images(i)) River[j] = true; }
}
