// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Rts.Game.Ui.Card;

namespace Rts.Game.Tests.Ui.Card;

/// <summary>M10-16: portrait files are found by rules id, must be 96x96 RGBA, and never change the card layout.</summary>
public sealed class PortraitFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "s24card_" + Guid.NewGuid().ToString("N"));

    public PortraitFilesTests() => Directory.CreateDirectory(_root);

    public void Dispose() { try { Directory.Delete(_root, true); } catch (IOException) { } }

    /// <summary>Writes a PNG signature plus an IHDR chunk header (enough for the validator; no pixel data).</summary>
    private void Write(string id, int w, int h, byte depth = 8, byte colour = 6)
    {
        var b = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };
        foreach (int v in new[] { w, h }) b.AddRange(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });
        b.AddRange(new[] { depth, colour, (byte)0, (byte)0, (byte)0 });
        File.WriteAllBytes(Path.Combine(_root, id + ".png"), b.ToArray());
    }

    [Fact]
    public void Valid_file_is_found() { Write("auth_rifleman", 96, 96); Assert.Equal(Path.Combine(_root, "auth_rifleman.png"), PortraitFiles.Resolve(_root, "auth_rifleman")); }

    [Fact]
    public void Missing_file_is_no_portrait() => Assert.Null(PortraitFiles.Resolve(_root, "auth_rifleman"));

    [Fact]
    public void Missing_folder_is_no_portrait() => Assert.Null(PortraitFiles.Resolve(Path.Combine(_root, "nope"), "x"));

    [Theory]
    [InlineData(64, 64, 8, 6)]
    [InlineData(96, 64, 8, 6)]
    [InlineData(96, 96, 8, 2)]   // RGB, no alpha
    [InlineData(96, 96, 16, 6)]
    public void Wrong_format_is_no_portrait(int w, int h, int depth, int colour)
    {
        Write("u", w, h, (byte)depth, (byte)colour);
        Assert.Null(PortraitFiles.Resolve(_root, "u"));
    }

    [Fact]
    public void Garbage_file_is_no_portrait() { File.WriteAllText(Path.Combine(_root, "g.png"), "not a png at all, just text of some length"); Assert.Null(PortraitFiles.Resolve(_root, "g")); }

    [Theory]
    [InlineData("")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    public void Unsafe_ids_are_rejected(string id) => Assert.Null(PortraitFiles.Resolve(_root, id));

    [Fact]
    public void Root_sits_beside_the_rules_folder() =>
        Assert.Equal(Path.Combine(Path.GetFullPath("/x/data"), "portraits"), PortraitFiles.RootFor("/x/data/rules"));

    [Fact]
    public void Layout_has_the_portrait_square_left_of_the_text_whatever_the_art()
    {
        var map = new Rectangle(0, 0, 1288, 900);
        CardLayout l = CardLayout.Compute(map, 1.5f, 18, CardKind.Unit, 5, 0);
        Assert.True(l.Portrait.Width > 0 && l.Portrait.Width == l.Portrait.Height);
        Assert.True(l.Portrait.Right <= l.Text.Left);
        // portrait art is looked up per id; the layout is a function of the model shape only, so with or without a file it is identical
        Write("auth_rifleman", 96, 96);
        Assert.Equal(l.Portrait, CardLayout.Compute(map, 1.5f, 18, CardKind.Unit, 5, 0).Portrait);
        Assert.Equal(l.Text, CardLayout.Compute(map, 1.5f, 18, CardKind.Unit, 5, 0).Text);
    }
}
