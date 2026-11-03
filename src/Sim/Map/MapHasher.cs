using System.IO.Hashing;
using System.Text;

namespace Rts.Sim.Map;

/// <summary>Computes the identity hash of a starting map: XxHash64 over name, size and all cell layers.</summary>
public static class MapHasher
{
    public static ulong Compute(MapData map)
    {
        ArgumentNullException.ThrowIfNull(map);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(map.Name);
            w.Write(map.Width); w.Write(map.Height);
            for (int i = 0; i < map.Template.Count; i++) w.Write(map.Template[i]);
            for (int i = 0; i < map.Land.Count; i++) w.Write((byte)map.Land[i]);
            for (int i = 0; i < map.HeightLevel.Count; i++) w.Write(map.HeightLevel[i]);
            for (int i = 0; i < map.Overlay.Count; i++) w.Write((byte)map.Overlay[i]);
            for (int i = 0; i < map.Density.Count; i++) w.Write(map.Density[i]);
        }
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }
}
