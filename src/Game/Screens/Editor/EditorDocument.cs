// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Screens.Editor;

/// <summary>
/// The map being edited: mutable cell layers, start positions and features, plus the metadata that goes to <see cref="MapMeta"/>.
/// A builder for <see cref="MapData"/>; it has no sim code and no drawing. Edits go through <see cref="EditorSession"/> so they can be undone.
/// </summary>
public sealed class EditorDocument
{
    /// <summary>Most start positions a map may have.</summary>
    public const int MaxStarts = 4;

    /// <summary>Smallest and largest map side the editor makes.</summary>
    public const int MinSize = 32, MaxSize = 128;

    private EditorDocument(string name, string biome, int width, int height)
    {
        Name = name; Biome = biome; Width = width; Height = height;
        int n = width * height;
        Template = new ushort[n]; Land = new LandType[n]; HeightLevel = new byte[n]; Overlay = new OverlayKind[n]; Density = new byte[n];
    }

    /// <summary>Map name (part of the map hash).</summary>
    public string Name { get; set; }

    /// <summary>Biome key as in <see cref="MapFile.BiomeKeys"/> (also the theater), or "" for none.</summary>
    public string Biome { get; set; }

    /// <summary>Developer fixture flag (<c>[map] dev = true</c>), kept so re-saving a dev map keeps it.</summary>
    public bool Dev { get; set; }

    /// <summary>Declared symmetry, saved in <c>[map] symmetry</c> and checked by the validator.</summary>
    public MapSymmetry Symmetry { get; set; }

    /// <summary>Generator provenance carried through from an opened generated map; null for a hand-made map.</summary>
    public MapGenParams? Generator { get; set; }

    /// <summary>Width in cells.</summary>
    public int Width { get; }

    /// <summary>Height in cells.</summary>
    public int Height { get; }

    /// <summary>Template layer (the editor leaves it as loaded / zero).</summary>
    public ushort[] Template { get; }

    /// <summary>Land type per cell, row-major.</summary>
    public LandType[] Land { get; }

    /// <summary>Height level 0..3 per cell.</summary>
    public byte[] HeightLevel { get; }

    /// <summary>Overlay per cell.</summary>
    public OverlayKind[] Overlay { get; }

    /// <summary>Resource density per cell.</summary>
    public byte[] Density { get; }

    /// <summary>Start positions in player order (contiguous, at most <see cref="MaxStarts"/>).</summary>
    public List<CellCoord> Starts { get; private set; } = new();

    /// <summary>Neutral features in placement order.</summary>
    public List<MapFeature> Features { get; private set; } = new();

    /// <summary>M7-14: Expedition convoy edge cells, kept as loaded so a save does not drop them.</summary>
    public List<CellCoord> ConvoyEdge { get; private set; } = new();

    /// <summary>M7-14: Expedition landing marker cells, kept as loaded so a save does not drop them.</summary>
    public List<CellCoord> Landing { get; private set; } = new();

    /// <summary>Index of a cell.</summary>
    public int IndexOf(int x, int y) => y * Width + x;

    /// <summary>True when the cell is on the map.</summary>
    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>A new all-<see cref="LandType.Clear"/> map with no starts. Throws for a size outside 32..128.</summary>
    public static EditorDocument Create(string name, string biome, int width, int height)
    {
        if (width < MinSize || width > MaxSize || height < MinSize || height > MaxSize)
            throw new ArgumentOutOfRangeException(nameof(width), $"map size must be {MinSize}..{MaxSize}");
        return new EditorDocument(name, biome, width, height);
    }

    /// <summary>The side of S / M / L maps (64 / 96 / 128).</summary>
    public static int SideFor(MapSize size) => MapGenerator.CellsFor(size);

    /// <summary>Copies an existing map and its metadata into a document.</summary>
    public static EditorDocument From(MapData map, MapMeta meta)
    {
        var d = new EditorDocument(map.Name, meta.Biome, map.Width, map.Height) { Symmetry = meta.Symmetry, Generator = meta.Generator, Dev = meta.Dev };
        for (int i = 0; i < map.Width * map.Height; i++)
        {
            d.Template[i] = map.Template[i]; d.Land[i] = map.Land[i]; d.HeightLevel[i] = map.HeightLevel[i];
            d.Overlay[i] = map.Overlay[i]; d.Density[i] = map.Density[i];
        }
        d.Starts = new List<CellCoord>(map.StartPositions);
        d.Features = new List<MapFeature>(meta.Features);
        d.ConvoyEdge = new List<CellCoord>(meta.ConvoyEdge);
        d.Landing = new List<CellCoord>(meta.Landing);
        return d;
    }

    /// <summary>Builds the immutable sim map from the current state (copies the arrays).</summary>
    public MapData ToMapData() => new(Name, string.IsNullOrEmpty(Biome) ? "rust_belt" : Biome, Width, Height,
        (ushort[])Template.Clone(), (LandType[])Land.Clone(), (byte[])HeightLevel.Clone(), (OverlayKind[])Overlay.Clone(),
        (byte[])Density.Clone(), Starts.ToArray());

    /// <summary>Builds the metadata for saving and validating.</summary>
    public MapMeta ToMeta() => new(Biome, Symmetry, Math.Max(1, Starts.Count), Generator, Features.ToArray())
    {
        ConvoyEdge = ConvoyEdge.ToArray(),
        Landing = Landing.ToArray(),
        Dev = Dev,
    };

    internal void SetStarts(IEnumerable<CellCoord> starts) => Starts = new List<CellCoord>(starts);

    internal void SetFeatures(IEnumerable<MapFeature> features) => Features = new List<MapFeature>(features);
}
