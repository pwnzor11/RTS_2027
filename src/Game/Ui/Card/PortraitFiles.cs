// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Card;

/// <summary>
/// Where selection-card portraits live and which files count (M10-16): <c>data/portraits/&lt;id&gt;.png</c>, 96x96 RGBA. Pure file logic
/// with no graphics, so it is unit-tested; <see cref="PortraitProvider"/> turns a valid file into a texture. A missing, wrong-sized or
/// unreadable file is simply "no portrait" and the card is drawn exactly as before.
/// </summary>
public static class PortraitFiles
{
    /// <summary>Required width and height in pixels.</summary>
    public const int Size = 96;

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    /// <summary>The portrait folder that belongs to a rules folder (<c>data/rules</c> gives <c>data/portraits</c>).</summary>
    /// <param name="rulesDir">The rules directory.</param>
    public static string RootFor(string rulesDir) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rulesDir)) ?? ".", "portraits");

    /// <summary>The file to load for a rules id, or null when the id is empty or not a plain file name or no valid file exists.</summary>
    /// <param name="root">The portrait folder.</param>
    /// <param name="id">Rules id of the unit or building.</param>
    public static string? Resolve(string root, string id)
    {
        if (string.IsNullOrEmpty(id) || id.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || id.Contains("..", StringComparison.Ordinal)) return null;
        string path = Path.Combine(root, id + ".png");
        return File.Exists(path) && IsValid(path) ? path : null;
    }

    /// <summary>True when the file is a PNG of 96x96 pixels with an alpha channel (colour type 6, 8 bits).</summary>
    /// <param name="path">File to check.</param>
    public static bool IsValid(string path)
    {
        try
        {
            using FileStream s = File.OpenRead(path);
            Span<byte> head = stackalloc byte[26];
            if (s.Read(head) < head.Length) return false;
            if (!head[..8].SequenceEqual(PngSignature)) return false;
            if (head[12] != (byte)'I' || head[13] != (byte)'H' || head[14] != (byte)'D' || head[15] != (byte)'R') return false;
            int w = ReadInt(head[16..]), h = ReadInt(head[20..]);
            return w == Size && h == Size && head[24] == 8 && head[25] == 6;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static int ReadInt(ReadOnlySpan<byte> b) => (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
}
