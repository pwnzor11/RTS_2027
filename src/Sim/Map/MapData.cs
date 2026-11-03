using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Map;

/// <summary>
/// Immutable starting map handed to the sim. Arrays are row-major, index = y * Width + x.
/// CONTRACT (lead). M1-01 adds file IO in Rts.Data and the runtime MapState in this folder.
/// </summary>
public sealed class MapData
{
    public MapData(string name, string theater, int width, int height, ushort[] template, LandType[] land,
        byte[] height_, OverlayKind[] overlay, byte[] density, IReadOnlyList<CellCoord> startPositions)
    {
        if (width < 1 || width > SimConstants.MaxMapCells || height < 1 || height > SimConstants.MaxMapCells)
            throw new ArgumentException("Map size must be 1..128 cells per axis.");
        int n = width * height;
        if (template.Length != n || land.Length != n || height_.Length != n || overlay.Length != n || density.Length != n)
            throw new ArgumentException("Every cell layer must have Width × Height entries.");
        Name = name; Theater = theater; Width = width; Height = height;
        Template = template; Land = land; HeightLevel = height_; Overlay = overlay; Density = density;
        StartPositions = startPositions;
    }

    public string Name { get; }
    public string Theater { get; }
    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<ushort> Template { get; }
    public IReadOnlyList<LandType> Land { get; }
    public IReadOnlyList<byte> HeightLevel { get; }
    public IReadOnlyList<OverlayKind> Overlay { get; }
    public IReadOnlyList<byte> Density { get; }
    public IReadOnlyList<CellCoord> StartPositions { get; }

    public int IndexOf(CellCoord c) => c.Y * Width + c.X;
    public bool Contains(CellCoord c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

    /// <summary>All-clear map, no overlays, two start positions in opposite corners.</summary>
    public static MapData CreateFlat(int width, int height, string name = "flat")
    {
        int n = width * height;
        return new MapData(name, "rust_belt", width, height, new ushort[n], new LandType[n], new byte[n],
            new OverlayKind[n], new byte[n],
            new[] { new CellCoord(2, 2), new CellCoord(width - 3, height - 3) });
    }
}
