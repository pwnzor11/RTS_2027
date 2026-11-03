using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Map;

/// <summary>
/// Mutable runtime copy of the map layers that can change during play (overlay, density). Minimal for now;
/// M1-01 grows it. Registered as the "map" state part.
/// </summary>
public sealed class MapState : ISimStatePart
{
    private readonly OverlayKind[] _overlay;
    private readonly byte[] _density;

    public MapState(MapData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        Width = data.Width;
        Height = data.Height;
        _overlay = new OverlayKind[data.Overlay.Count];
        _density = new byte[data.Density.Count];
        for (int i = 0; i < _overlay.Length; i++) { _overlay[i] = data.Overlay[i]; _density[i] = data.Density[i]; }
    }

    public string Name => "map";
    public int Width { get; }
    public int Height { get; }

    /// <summary>The immutable starting map this state was copied from.</summary>
    public MapData Data { get; }

    public OverlayKind OverlayAt(CellCoord c) => _overlay[Data.IndexOf(c)];
    public byte DensityAt(CellCoord c) => _density[Data.IndexOf(c)];

    public void Write(BinaryWriter w)
    {
        w.Write(Width); w.Write(Height);
        for (int i = 0; i < _overlay.Length; i++) w.Write((byte)_overlay[i]);
        w.Write(_density);
    }

    public void Read(BinaryReader r)
    {
        if (r.ReadInt32() != Width || r.ReadInt32() != Height) throw new InvalidDataException("Map size mismatch.");
        for (int i = 0; i < _overlay.Length; i++) _overlay[i] = (OverlayKind)r.ReadByte();
        byte[] d = r.ReadBytes(_density.Length);
        if (d.Length != _density.Length) throw new EndOfStreamException();
        d.CopyTo(_density, 0);
    }
}
