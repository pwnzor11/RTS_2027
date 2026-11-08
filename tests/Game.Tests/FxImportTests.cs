// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sprites;

namespace Rts.Game.Tests;

/// <summary>S15 art-packs pilot: the imported effect sheets are well-formed, the new kinds exist, and the classic comparison set swaps in.</summary>
public sealed class FxImportTests
{
    private static readonly string[] Imported = { "explosion_large", "explosion_small", "explosion_energy", "lightning", "lightning_fork", "fire_small", "fire_large" };

    private static string? SpriteRoot()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "sprites"));
        return Directory.Exists(root) ? root : null;
    }

    private static (int W, int H) PngSize(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        int Be(int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];
        return (Be(16), Be(20));
    }

    [Fact]
    public void Imported_effect_sheets_match_their_manifests()
    {
        string? root = SpriteRoot();
        if (root is null) return;
        using SpriteLibrary lib = SpriteLibrary.Scan(root);
        foreach (string id in Imported)
        {
            SpriteManifest m = lib.Manifest(id);
            (int w, int h) = PngSize(Path.Combine(Path.GetDirectoryName(m.SourcePath)!, m.SheetFile));
            Assert.Equal(m.FrameW * m.State("play").Frames, w);
            Assert.Equal(m.FrameH, h);
        }

        Assert.False(lib.Manifest("explosion_energy").State("play").Loop);
        Assert.False(lib.Manifest("lightning").State("play").Loop);
        Assert.False(lib.Manifest("lightning_fork").State("play").Loop);
        Assert.True(lib.Manifest("fire_small").State("play").Loop);
        Assert.True(lib.Manifest("fire_large").State("play").Loop);
    }

    [Fact]
    public void Classic_folder_holds_every_old_effect_and_replaces_them_when_asked()
    {
        string? root = SpriteRoot();
        if (root is null) return;
        string classic = ClassicFx.FolderFor(root);
        Assert.True(Directory.Exists(classic));
        foreach (string id in new[] { "fire_small", "fire_large", "smoke_plume", "sparks", "explosion_small", "explosion_large" })
            Assert.True(File.Exists(Path.Combine(classic, id + ".sprite.toml")), id);

        using SpriteLibrary lib = SpriteLibrary.Scan(root);
        var dict = new SortedDictionary<string, SpriteManifest>(StringComparer.Ordinal);
        foreach (string id in lib.Ids) dict[id] = lib.Manifest(id);
        int newFrames = dict["explosion_large"].State("play").Frames;
        ClassicFx.Apply(root, dict, false);
        Assert.Equal(newFrames, dict["explosion_large"].State("play").Frames);
        ClassicFx.Apply(root, dict, true);
        Assert.Equal(12, dict["explosion_large"].State("play").Frames);     // the procedural sheet had 12 frames
        Assert.DoesNotContain("explosion_energy", Directory.GetFiles(classic).Select(Path.GetFileNameWithoutExtension));
        Assert.Contains("explosion_energy", dict.Keys);                    // kinds added by the import stay available
    }
}
