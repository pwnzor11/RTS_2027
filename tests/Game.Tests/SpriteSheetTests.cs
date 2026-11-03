// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Microsoft.Xna.Framework;
using Rts.Game.Sprites;

namespace Rts.Game.Tests;

/// <summary>Checks house-paint extraction, the CPU blend, manifest parsing and the sprite library. No GPU needed.</summary>
public sealed class HouseSplitTests
{
    private static readonly uint[] Ramp =
    {
        0x400040, 0x5B125B, 0x772577, 0x923792, 0xAD49AD, 0xC85BC8, 0xE46EE4, 0xFF80FF,
    };

    // 4x4 synthetic sheet. Six house pixels: four shades at full alpha on row 0 (the lightest twice), shade 5 at half
    // alpha at (0,1), shade 4 at (0,2). A transparent pixel with a ramp colour at (1,1) is not house paint.
    private static byte[] Synthetic()
    {
        var px = new byte[4 * 4 * 4];
        void Set(int x, int y, uint rgb, byte alpha)
        {
            int i = (y * 4 + x) * 4;
            px[i] = (byte)(rgb >> 16);
            px[i + 1] = (byte)(rgb >> 8);
            px[i + 2] = (byte)rgb;
            px[i + 3] = alpha;
        }
        Set(0, 0, 0x400040, 255);
        Set(1, 0, 0x923792, 255);
        Set(2, 0, 0xFF80FF, 255);
        Set(3, 0, 0xFF80FF, 255);
        Set(0, 1, 0xC85BC8, 128);
        Set(1, 1, 0x400040, 0);
        Set(2, 1, 0x112233, 255);
        Set(3, 1, 0xFFFFFF, 255);
        Set(0, 2, 0xAD49AD, 255);
        return px;
    }

    private static int Index(int x, int y) => (y * 4 + x) * 4;

    [Fact]
    public void Split_counts_the_ramp_pixels_exactly()
    {
        byte[] source = Synthetic();
        var baseRgba = new byte[source.Length];
        var mask = new byte[source.Length];

        int count = HouseSplit.Split(source, Ramp, baseRgba, mask);

        Assert.Equal(6, count);
        int masked = 0;
        for (int i = 3; i < mask.Length; i += 4)
        {
            if (mask[i] > 0) masked++;
        }
        Assert.Equal(6, masked);
    }

    [Fact]
    public void Base_is_transparent_exactly_where_the_house_pixels_were()
    {
        byte[] source = Synthetic();
        var baseRgba = new byte[source.Length];
        var mask = new byte[source.Length];
        HouseSplit.Split(source, Ramp, baseRgba, mask);

        int[][] house = { new[] { 0, 0 }, new[] { 1, 0 }, new[] { 2, 0 }, new[] { 3, 0 }, new[] { 0, 1 }, new[] { 0, 2 } };
        foreach (int[] p in house)
        {
            Assert.Equal(0, baseRgba[Index(p[0], p[1]) + 3]);
        }

        // Non-house pixels are copied unchanged, including the transparent ramp-coloured pixel at (1,1).
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                if (Array.Exists(house, p => p[0] == x && p[1] == y)) continue;
                int i = Index(x, y);
                Assert.Equal(source[i], baseRgba[i]);
                Assert.Equal(source[i + 1], baseRgba[i + 1]);
                Assert.Equal(source[i + 2], baseRgba[i + 2]);
                Assert.Equal(source[i + 3], baseRgba[i + 3]);
            }
        }
    }

    [Fact]
    public void Mask_holds_the_shade_greys_and_keeps_alpha()
    {
        byte[] source = Synthetic();
        var baseRgba = new byte[source.Length];
        var mask = new byte[source.Length];
        HouseSplit.Split(source, Ramp, baseRgba, mask);
        byte[] greys = HouseSplit.ShadeGreys(Ramp);

        int i = Index(0, 0);
        Assert.Equal(greys[0], mask[i]);
        Assert.Equal(greys[0], mask[i + 1]);
        Assert.Equal(greys[0], mask[i + 2]);
        Assert.Equal(255, mask[i + 3]);

        Assert.Equal(greys[7], mask[Index(2, 0)]);
        Assert.Equal(greys[5], mask[Index(0, 1)]);
        Assert.Equal(128, mask[Index(0, 1) + 3]);
        Assert.Equal(0, mask[Index(1, 1) + 3]);
    }

    [Fact]
    public void Shade_greys_run_dark_to_light_and_the_lightest_is_full()
    {
        byte[] greys = HouseSplit.ShadeGreys(Ramp);

        Assert.Equal(8, greys.Length);
        Assert.Equal(255, greys[7]);
        Assert.Equal(37, greys[0]);
        for (int i = 1; i < greys.Length; i++)
        {
            Assert.True(greys[i] > greys[i - 1], $"shade {i} should be lighter than shade {i - 1}");
        }
    }

    [Fact]
    public void Split_rejects_mismatched_buffers()
    {
        byte[] source = Synthetic();
        Assert.Throws<ArgumentException>(() => HouseSplit.Split(source, Ramp, new byte[8], new byte[source.Length]));
    }

    [Fact]
    public void Compose_tints_the_mask_and_leaves_other_pixels_alone()
    {
        var baseRgba = new byte[] { 0, 0, 0, 0, 10, 20, 30, 255 };
        var mask = new byte[] { 128, 128, 128, 255, 0, 0, 0, 0 };
        var dst = new byte[8];

        HouseSplit.Compose(baseRgba, mask, new Color(200, 100, 50), dst);

        // round(128 * 200 / 255) = 100, round(128 * 100 / 255) = 50, round(128 * 50 / 255) = 25.
        Assert.Equal(new byte[] { 100, 50, 25, 255 }, dst[..4]);
        Assert.Equal(new byte[] { 10, 20, 30, 255 }, dst[4..]);
    }

    [Fact]
    public void Compose_full_light_shade_shows_the_house_colour_exactly()
    {
        byte[] greys = HouseSplit.ShadeGreys(Ramp);
        var baseRgba = new byte[4];
        var mask = new byte[] { greys[7], greys[7], greys[7], 255 };
        var dst = new byte[4];
        var colour = new Color(0x2F, 0x76, 0xC0);

        HouseSplit.Compose(baseRgba, mask, colour, dst);

        Assert.Equal(new byte[] { 0x2F, 0x76, 0xC0, 255 }, dst);
    }

    [Fact]
    public void Premultiply_scales_partial_alpha_and_keeps_opaque_pixels()
    {
        var rgba = new byte[] { 200, 100, 50, 128, 200, 100, 50, 255 };
        HouseSplit.Premultiply(rgba);
        Assert.Equal(new byte[] { 100, 50, 25, 128, 200, 100, 50, 255 }, rgba);
    }
}

/// <summary>Checks manifest parsing: states, defaults, packing, and clear errors for bad values.</summary>
public sealed class SpriteManifestTests
{
    private const string SourcePath = "test/vehicles/sample.sprite.toml";

    private static string Ramp(int entries = 8)
    {
        string[] all = { "#400040", "#5B125B", "#772577", "#923792", "#AD49AD", "#C85BC8", "#E46EE4", "#FF80FF" };
        return string.Join(", ", all.Take(entries).Select(c => $"\"{c}\""));
    }

    private static string Valid() => string.Join("\n",
        "file = \"sample.png\"",
        "frame_w = 32",
        "frame_h = 32",
        "origin_x = 16",
        "origin_y = 24",
        "",
        "[house]",
        $"ramp = [{Ramp()}]",
        "",
        "[[state]]",
        "name = \"idle\"",
        "facings = 8",
        "frames = 2",
        "first = 0",
        "ticks_per_frame = 3",
        "",
        "[[state]]",
        "name = \"build\"",
        "facings = 1");

    [Fact]
    public void Parses_states_and_fills_defaults()
    {
        SpriteManifest m = SpriteManifest.Parse(Valid(), "sample", SourcePath);

        Assert.Equal("sample", m.Id);
        Assert.Equal("sample.png", m.SheetFile);
        Assert.Equal(32, m.FrameW);
        Assert.Equal(16, m.OriginX);
        Assert.Equal(24, m.OriginY);
        Assert.Equal(8, m.HouseRamp.Count);
        Assert.Equal(0xFF80FFu, m.HouseRamp[7]);

        Assert.Equal(2, m.States.Count);
        SpriteState idle = m.State("idle");
        Assert.Equal(new SpriteState("idle", 8, 2, 0, 3), idle);
        Assert.Equal(16, idle.CellCount);

        // Defaults: frames 1, ticks_per_frame 1, and first packs after the previous state (16).
        SpriteState build = m.State("build");
        Assert.Equal(new SpriteState("build", 1, 1, 16, 1), build);
    }

    [Fact]
    public void Missing_house_table_means_no_house_paint()
    {
        string text = Valid().Replace($"[house]\nramp = [{Ramp()}]\n", "");
        SpriteManifest m = SpriteManifest.Parse(text, "sample", SourcePath);
        Assert.Empty(m.HouseRamp);
    }

    [Fact]
    public void Frame_index_maps_facing_and_animation()
    {
        SpriteManifest m = SpriteManifest.Parse(Valid(), "sample", SourcePath);

        // idle: 8 facings, 2 frames each, 3 ticks per frame, starting at cell 0. Facing index f, step k -> 2f + k.
        Assert.Equal(0, m.FrameIndex("idle", 0, 0));
        Assert.Equal(1, m.FrameIndex("idle", 0, 3));   // second animation frame
        Assert.Equal(0, m.FrameIndex("idle", 0, 6));   // wraps
        Assert.Equal(2, m.FrameIndex("idle", 32, 0));  // byte 32 is facing index 1 of 8 (east-ish 45 deg)
        Assert.Equal(14, m.FrameIndex("idle", 224, 0)); // byte 224 is facing 7 of 8 -> 2*7
        Assert.Equal(16, m.FrameIndex("build", 200, 0)); // single facing, no animation
    }

    [Fact]
    public void Unknown_state_lists_the_known_states()
    {
        SpriteManifest m = SpriteManifest.Parse(Valid(), "sample", SourcePath);
        var error = Assert.Throws<KeyNotFoundException>(() => m.State("walk"));
        Assert.Contains("no state 'walk'", error.Message);
        Assert.Contains("idle, build", error.Message);
    }

    [Theory]
    [InlineData("facings = 8", "facings = 12", "facings must be 1, 8, 16 or 32, found 12")]
    [InlineData("origin_x = 16", "origin_x = 40", "origin (40, 24) must lie inside the 32x32 frame")]
    [InlineData("frame_w = 32", "frame_w = 0", "'frame_w' must be greater than 0")]
    [InlineData("frame_w = 32", "frame_w = \"32\"", "'frame_w' must be a whole number of 0 or more")]
    [InlineData("frame_h = 32\n", "", "missing 'frame_h'")]
    [InlineData("frame_w = 32", "frame_x = 32", "unknown key 'frame_x' in top level")]
    [InlineData("frames = 2", "frames = 0", "state 'idle' frames must be at least 1")]
    [InlineData("ticks_per_frame = 3", "ticks_per_frame = 0", "state 'idle' ticks_per_frame must be at least 1")]
    [InlineData("name = \"build\"", "name = \"idle\"", "state 'idle' is defined twice")]
    [InlineData("name = \"build\"\n", "name = \"build\"\nfirst = 4\n", "state 'idle' and state 'build' use the same frames")]
    [InlineData("name = \"build\"\n", "name = \"build\"\nspeed = 2\n", "unknown key 'speed' in [[state]]")]
    [InlineData("\"#FF80FF\"]", "\"#FF80GG\"]", "ramp entry 7 must be #RRGGBB")]
    [InlineData("\"#FF80FF\"]", "\"FF80FF\"]", "ramp entry 7 must be #RRGGBB")]
    [InlineData("file = \"sample.png\"", "", "missing 'file'")]
    [InlineData("name = \"build\"\n", "", "missing 'name'")]
    public void Bad_values_are_rejected_with_path_and_reason(string find, string replace, string expected)
    {
        string text = Valid();
        Assert.Contains(find, text);
        string bad = text.Replace(find, replace);

        var error = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(bad, "sample", SourcePath));
        Assert.Contains(expected, error.Message);
        Assert.StartsWith(SourcePath + ":", error.Message);
    }

    [Fact]
    public void House_ramp_must_have_eight_entries()
    {
        string bad = Valid().Replace(Ramp(), Ramp(7));
        var error = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(bad, "sample", SourcePath));
        Assert.Contains("[house] ramp must have 8 colours, found 7", error.Message);
    }

    [Fact]
    public void A_manifest_without_states_is_rejected()
    {
        string text = Valid()[..Valid().IndexOf("[[state]]", StringComparison.Ordinal)];
        var error = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(text, "sample", SourcePath));
        Assert.Contains("needs at least one [[state]] table", error.Message);
    }

    [Fact]
    public void Invalid_toml_is_reported_with_the_path()
    {
        var error = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse("this is = = not toml", "sample", SourcePath));
        Assert.Contains("not valid TOML", error.Message);
        Assert.StartsWith(SourcePath + ":", error.Message);
    }

    [Fact]
    public void The_shipped_tank_manifest_parses()
    {
        string file = RepoFile("data", "sprites", "vehicles", "tank_v2.sprite.toml");
        SpriteManifest m = SpriteManifest.Load(file);

        Assert.Equal("tank_v2", m.Id);
        Assert.Equal("tank_v2.png", m.SheetFile);
        Assert.Equal(96, m.FrameW);
        Assert.Equal(96, m.FrameH);
        Assert.Equal(48, m.OriginX);
        Assert.Equal(64, m.OriginY);
        Assert.Equal(8, m.HouseRamp.Count);
        SpriteState idle = Assert.Single(m.States);
        Assert.Equal(new SpriteState("idle", 16, 1, 0, 1), idle);
    }

    /// <summary>Path of a file under the repository root, found by walking up from the test binaries.</summary>
    internal static string RepoFile(params string[] parts)
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(System.IO.Path.Combine(dir, "RTS_2027.slnx")))
        {
            dir = System.IO.Path.GetDirectoryName(dir) ?? throw new DirectoryNotFoundException("repo root not found");
        }
        foreach (string part in parts)
        {
            dir = System.IO.Path.Combine(dir, part);
        }
        return dir;
    }
}

/// <summary>Checks the sprite library: id index, lookup, duplicate ids, and the not-yet-loaded state.</summary>
public sealed class SpriteLibraryTests : IDisposable
{
    private readonly string _root;

    public SpriteLibraryTests()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rts_sprite_library_" + Guid.NewGuid().ToString("N"));
        Write("vehicles/tank_v2.sprite.toml", Manifest(frameW: 96));
        Write("buildings/depot.sprite.toml", Manifest(frameW: 64));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Scan_indexes_every_manifest_by_its_file_stem()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(_root);

        Assert.Equal(new[] { "depot", "tank_v2" }, library.Ids);
        Assert.Equal(96, library.Manifest("tank_v2").FrameW);
        Assert.Equal(64, library.Manifest("depot").FrameW);
    }

    [Fact]
    public void Unknown_id_lists_the_known_ids()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(_root);
        var error = Assert.Throws<KeyNotFoundException>(() => library.Manifest("harvester"));
        Assert.Contains("no sprite 'harvester' (known: depot, tank_v2)", error.Message);
    }

    [Fact]
    public void A_sheet_is_not_available_before_LoadAll()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(_root);
        Assert.Throws<InvalidOperationException>(() => library.Sheet("tank_v2"));
        Assert.Throws<KeyNotFoundException>(() => library.Sheet("harvester"));
    }

    [Fact]
    public void Two_manifests_with_one_id_are_rejected()
    {
        Write("other/tank_v2.sprite.toml", Manifest(frameW: 48));
        var error = Assert.Throws<InvalidDataException>(() => SpriteLibrary.Scan(_root));
        Assert.Contains("sprite id 'tank_v2' is defined twice", error.Message);
    }

    [Fact]
    public void A_missing_folder_is_reported()
    {
        Assert.Throws<DirectoryNotFoundException>(() => SpriteLibrary.Scan(_root + "_missing"));
    }

    [Fact]
    public void Shipped_sprites_are_all_found()
    {
        string dir = SpriteManifestTests.RepoFile("data", "sprites");
        using SpriteLibrary library = SpriteLibrary.Scan(dir);
        Assert.Contains("tank_v2", library.Ids);
    }

    private void Write(string relative, string text)
    {
        string path = System.IO.Path.Combine(_root, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private static string Manifest(int frameW) => string.Join("\n",
        "file = \"sheet.png\"",
        $"frame_w = {frameW}",
        $"frame_h = {frameW}",
        "origin_x = 0",
        "origin_y = 0",
        "",
        "[[state]]",
        "name = \"idle\"",
        "facings = 1");
}

/// <summary>Checks the palette the house colours are drawn from.</summary>
public sealed class PlayerColoursTests
{
    [Fact]
    public void The_first_eight_house_colours_are_distinct()
    {
        var colours = Enumerable.Range(0, PlayerColours.Count).Select(PlayerColours.Base).ToArray();
        Assert.Equal(8, PlayerColours.Count);
        Assert.Equal(colours.Length, colours.Distinct().Count());
    }

    [Fact]
    public void Slots_beyond_eight_wrap_and_negative_slots_are_rejected()
    {
        Assert.Equal(PlayerColours.Base(0), PlayerColours.Base(8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerColours.Base(-1));
    }

    [Fact]
    public void Authority_is_green_and_directorate_is_rust_red()
    {
        Assert.Equal(new Color(0x4F, 0xA0, 0x4A), PlayerColours.Base(0));
        Assert.Equal(new Color(0xC0, 0x46, 0x2B), PlayerColours.Base(1));
    }
}

/// <summary>
/// Proof, not a regression check: composes the shipped tank for players 0 to 7 with the CPU replica of the GPU
/// blend and writes a raw image. Runs only when RTS_PROOF_IN and RTS_PROOF_OUT are set; see scratch/ in the
/// worktree for the decode and encode steps around it.
/// </summary>
public sealed class HouseColourProofTests
{
    [Fact]
    public void Compose_the_tank_for_eight_houses()
    {
        string? input = Environment.GetEnvironmentVariable("RTS_PROOF_IN");
        string? output = Environment.GetEnvironmentVariable("RTS_PROOF_OUT");
        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(output)) return;

        SpriteManifest manifest = SpriteManifest.Load(SpriteManifestTests.RepoFile("data", "sprites", "vehicles", "tank_v2.sprite.toml"));
        byte[] raw = File.ReadAllBytes(input);
        int width = BitConverter.ToInt32(raw, 0);
        int height = BitConverter.ToInt32(raw, 4);
        byte[] pixels = raw[8..];
        Assert.Equal(width * height * 4, pixels.Length);

        var basePixels = new byte[pixels.Length];
        var maskPixels = new byte[pixels.Length];
        int houseCount = HouseSplit.Split(pixels, manifest.HouseRamp.ToArray(), basePixels, maskPixels);
        Assert.Equal(4086, houseCount);
        HouseSplit.Premultiply(basePixels);
        HouseSplit.Premultiply(maskPixels);

        // Two rows of eight tanks: facing 0 (north) and facing 4 (east, 90 deg on a 16-facing sheet).
        int fw = manifest.FrameW;
        int fh = manifest.FrameH;
        int canvasW = PlayerColours.Count * fw;
        int canvasH = 2 * fh;
        var canvas = new byte[canvasW * canvasH * 4];
        var background = new Color(0x30, 0x30, 0x30);

        for (int row = 0; row < 2; row++)
        {
            int facingByte = row == 0 ? 0 : 64;
            int frame = manifest.FrameIndex("idle", (byte)facingByte, tick: 0);
            int columns = width / fw;
            int frameX = (frame % columns) * fw;
            int frameY = (frame / columns) * fh;

            for (int player = 0; player < PlayerColours.Count; player++)
            {
                var frameBase = new byte[fw * fh * 4];
                var frameMask = new byte[fw * fh * 4];
                var composed = new byte[fw * fh * 4];
                for (int y = 0; y < fh; y++)
                {
                    int src = ((frameY + y) * width + frameX) * 4;
                    int dst = y * fw * 4;
                    Array.Copy(basePixels, src, frameBase, dst, fw * 4);
                    Array.Copy(maskPixels, src, frameMask, dst, fw * 4);
                }
                HouseSplit.Compose(frameBase, frameMask, PlayerColours.Base(player), composed);

                for (int y = 0; y < fh; y++)
                {
                    for (int x = 0; x < fw; x++)
                    {
                        int s = (y * fw + x) * 4;
                        int d = ((row * fh + y) * canvasW + player * fw + x) * 4;
                        int a = composed[s + 3];
                        // Composite the premultiplied tank over the background so the proof is easy to look at.
                        int[] bg = { background.R, background.G, background.B };
                        for (int c = 0; c < 3; c++)
                        {
                            int over = composed[s + c] + bg[c] * (255 - a) / 255;
                            canvas[d + c] = (byte)Math.Min(255, over);
                        }
                        canvas[d + 3] = 255;
                    }
                }
            }
        }

        using var stream = new MemoryStream();
        stream.Write(BitConverter.GetBytes(canvasW));
        stream.Write(BitConverter.GetBytes(canvasH));
        stream.Write(canvas);
        File.WriteAllBytes(output, stream.ToArray());
    }
}
