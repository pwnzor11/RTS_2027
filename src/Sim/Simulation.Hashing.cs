using System.IO.Hashing;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Sim;

// Hashing (M1-10, reworked M1.5-03 / review A-8). One serialisation pass feeds two XxHash64 streams at once: the full
// state hash and, for the part being written, that part's sub-hash. The full hash keeps its original definition
// (XxHash64 over tick, RNG state, then each part's name and data in canonical order), so existing hashes did not
// change. Nothing is buffered: the writer hashes straight into the sink, so a pass allocates nothing after warm-up.
public sealed partial class Simulation
{
    /// <summary>
    /// Per-part hashes in canonical part order, preceded by ("core", hash of the tick and RNG state). Each part's
    /// hash is XxHash64 over its <see cref="ISimStatePart.Write"/> output, so the parts together cover the state hash.
    /// Works for any set of registered parts.
    /// </summary>
    public IReadOnlyList<(string Part, ulong Hash)> ComputeSubHashes() => ComputeHashes(out _);

    /// <summary>
    /// Full state hash and per-part sub-hashes from one serialisation pass. <paramref name="full"/> equals
    /// <see cref="ComputeStateHash"/>; the list equals <see cref="ComputeSubHashes"/>.
    /// </summary>
    public IReadOnlyList<(string Part, ulong Hash)> ComputeHashes(out ulong full)
    {
        var list = new List<(string Part, ulong Hash)>(_parts.Length + 1);
        full = HashPass(list);
        return list;
    }

    /// <summary>As <see cref="ComputeHashes(out ulong)"/> but fills a caller-owned list (cleared first), so a caller
    /// that hashes often allocates nothing per call.</summary>
    public ulong ComputeHashes(List<(string Part, ulong Hash)> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        return HashPass(into);
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

    // Reused across calls; Simulation is single-threaded. Scratch, not state.
    private readonly HashSink _hashSink = new();
    private BinaryWriter? _hashWriter;

    private ulong HashPass(List<(string Part, ulong Hash)>? sub)
    {
        HashSink sink = _hashSink;
        BinaryWriter w = _hashWriter ??= new BinaryWriter(sink, Encoding.UTF8, leaveOpen: true);
        sink.Full.Reset();
        sink.Part.Reset();
        sink.PartActive = true;
        w.Write(Tick);
        (ulong state, ulong inc) = _rng.GetState();
        w.Write(state);
        w.Write(inc);
        w.Flush();
        sub?.Add(("core", sink.Part.GetCurrentHashAsUInt64()));
        foreach (ISimStatePart part in _parts)
        {
            sink.PartActive = false;           // the name belongs to the full hash only
            w.Write(part.Name);
            w.Flush();
            sink.Part.Reset();
            sink.PartActive = true;
            part.Write(w);
            w.Flush();
            sub?.Add((part.Name, sink.Part.GetCurrentHashAsUInt64()));
        }
        sink.PartActive = false;
        return sink.Full.GetCurrentHashAsUInt64();
    }

    /// <summary>Write-only stream that feeds every byte to the full hasher and, while a part is active, to the part hasher.</summary>
    private sealed class HashSink : Stream
    {
        public readonly XxHash64 Full = new();
        public readonly XxHash64 Part = new();
        public bool PartActive;

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Full.Append(buffer);
            if (PartActive) Part.Append(buffer);
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void WriteByte(byte value) => Write(new ReadOnlySpan<byte>(in value));
        public override void Flush() { }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
