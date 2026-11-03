using System.IO.Compression;
using System.Text;

namespace Rts.Sim.Serialization;

/// <summary>
/// The rules source files embedded in a replay or save: a deflate-compressed list of (relative name, text) pairs.
/// Compression lives in the file format only; it is never part of sim state or the hash.
/// </summary>
public static class RulesBlob
{
    /// <summary>Upper bound on the decompressed size, so a corrupt file cannot exhaust memory.</summary>
    public const int MaxUncompressedBytes = 64 * 1024 * 1024;

    /// <summary>Writes a length-prefixed compressed blob.</summary>
    public static void Write(BinaryWriter w, IReadOnlyList<(string Name, string Text)> files)
    {
        using var packed = new MemoryStream();
        using (var z = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        using (var bw = new BinaryWriter(z, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(files.Count);
            foreach ((string name, string text) in files) { bw.Write(name); bw.Write(text); }
        }
        w.Write((int)packed.Length);
        w.Write(packed.GetBuffer(), 0, (int)packed.Length);
    }

    /// <summary>Reads a blob written by <see cref="Write"/>. Throws InvalidDataException when it is corrupt.</summary>
    public static IReadOnlyList<(string Name, string Text)> Read(BinaryReader r)
    {
        int length = r.ReadInt32();
        if (length < 0 || length > MaxUncompressedBytes) throw new InvalidDataException("Bad embedded rules size.");
        byte[] packed = r.ReadBytes(length);
        if (packed.Length != length) throw new InvalidDataException("Truncated embedded rules.");
        try
        {
            using var raw = new MemoryStream();
            using (var z = new DeflateStream(new MemoryStream(packed), CompressionMode.Decompress))
            {
                var buffer = new byte[16 * 1024];
                int n;
                while ((n = z.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (raw.Length + n > MaxUncompressedBytes) throw new InvalidDataException("Embedded rules are too large.");
                    raw.Write(buffer, 0, n);
                }
            }
            raw.Position = 0;
            using var br = new BinaryReader(raw, Encoding.UTF8);
            int count = br.ReadInt32();
            if (count < 0 || count > 4096) throw new InvalidDataException("Bad embedded rules file count.");
            var files = new List<(string Name, string Text)>(count);
            for (int i = 0; i < count; i++) files.Add((br.ReadString(), br.ReadString()));
            return files;
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException or IOException or FormatException or ArgumentException)
        {
            throw new InvalidDataException("Corrupt embedded rules: " + e.Message);
        }
    }
}
