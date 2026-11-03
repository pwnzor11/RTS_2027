using System.IO.Hashing;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Sim;

// Sub-hashes (M1-10). Owner: desync tooling. One XxHash64 per state part so a mismatch names the part that diverged.
public sealed partial class Simulation
{
    /// <summary>
    /// Per-part hashes in canonical part order, preceded by ("core", hash of the tick and RNG state). Each part's
    /// hash is XxHash64 over its <see cref="ISimStatePart.Write"/> output, so the parts together cover the state hash.
    /// Works for any set of registered parts.
    /// </summary>
    public IReadOnlyList<(string Part, ulong Hash)> ComputeSubHashes()
    {
        var list = new List<(string Part, ulong Hash)>(_parts.Length + 1) { ("core", HashCore()) };
        foreach (ISimStatePart part in _parts) list.Add((part.Name, HashPart(part)));
        return list;
    }

    /// <summary>
    /// Name of the first part whose sub-hash differs between <paramref name="a"/> and <paramref name="b"/>, or null
    /// if they are identical. A length mismatch reports the first part present in only one list.
    /// </summary>
    public static string? FirstDifferentPart(IReadOnlyList<(string Part, ulong Hash)> a, IReadOnlyList<(string Part, ulong Hash)> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        int n = Math.Max(a.Count, b.Count);
        for (int i = 0; i < n; i++)
        {
            if (i >= a.Count) return b[i].Part;
            if (i >= b.Count) return a[i].Part;
            if (a[i].Part != b[i].Part || a[i].Hash != b[i].Hash) return a[i].Part;
        }
        return null;
    }

    private ulong HashCore()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(Tick);
            (ulong state, ulong inc) = _rng.GetState();
            w.Write(state);
            w.Write(inc);
        }
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }

    private static ulong HashPart(ISimStatePart part)
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true)) part.Write(w);
        return XxHash64.HashToUInt64(ms.GetBuffer().AsSpan(0, (int)ms.Length));
    }
}
