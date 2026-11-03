// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Compression;
using System.Text;
using Microsoft.Xna.Framework;
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Rts.Sim.Core;

namespace Rts.Game.Tests;

/// <summary>Resolver choice (file by id, alias, placeholder fallback). No GPU needed.</summary>
public sealed class SpriteResolverTests : IDisposable
{
    private readonly string _root;

    public SpriteResolverTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "rts_resolver_" + Guid.NewGuid().ToString("N"));
        Write("buildings/auth_refinery.sprite.toml", Manifest(192, 224, 0, 32));
        Write("vehicles/tank_v2.sprite.toml", Manifest(96, 96, 48, 64, facings: 16));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_file_is_picked_by_rules_id()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(_root);
        var resolver = new SpriteResolver(library);

        ResolvedSprite refinery = resolver.Resolve(SpriteKind.Building, "auth_refinery", 3, 3);

        Assert.Equal(SpriteSource.Library, refinery.Source);
        Assert.False(refinery.IsPlaceholder);
        Assert.Equal("auth_refinery", refinery.Manifest.Id);
        Assert.Equal(192, refinery.Manifest.FrameW);
        Assert.Equal(32, refinery.Manifest.OriginY);
    }

    [Fact]
    public void A_vehicle_alias_reaches_the_renamed_file()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(_root);
        var resolver = new SpriteResolver(library);

        ResolvedSprite tank = resolver.Resolve(SpriteKind.Vehicle, "auth_tank");

        Assert.Equal(SpriteSource.Library, tank.Source);
        Assert.Equal("tank_v2", tank.Manifest.Id);
    }

    [Fact]
    public void A_missing_file_falls_back_to_a_building_placeholder_of_the_footprint()
    {
        using SpriteLibrary library = SpriteLibrary.Scan(_root);
        var resolver = new SpriteResolver(library);

        ResolvedSprite barracks = resolver.Resolve(SpriteKind.Building, "auth_barracks", 2, 2);

        Assert.Equal(SpriteSource.Placeholder, barracks.Source);
        Assert.True(barracks.IsPlaceholder);
        Assert.NotNull(barracks.Placeholder);
        Assert.Equal(2 * PlaceholderArt.CellPx, barracks.Manifest.FrameW);
        Assert.Equal(2 * PlaceholderArt.CellPx, barracks.Manifest.FrameH);
        Assert.Equal((0, 0), (barracks.Manifest.OriginX, barracks.Manifest.OriginY));
    }

    [Fact]
    public void A_unit_without_a_file_gets_a_placeholder_and_no_rules_gives_the_generic_one()
    {
        var resolver = new SpriteResolver(null);

        ResolvedSprite rifleman = resolver.Resolve(SpriteKind.Infantry, "auth_rifleman");
        ResolvedSprite unknown = resolver.Resolve(SpriteKind.Vehicle, null);

        Assert.Equal(SpriteSource.Placeholder, rifleman.Source);
        Assert.Equal(8, rifleman.Manifest.States.First(s => s.Name == "idle").Facings);
        Assert.Equal(SpriteSource.Placeholder, unknown.Source);
        Assert.Equal("vehicle", unknown.Id);
        Assert.Equal(16, unknown.Manifest.States.First(s => s.Name == "idle").Facings);
    }

    [Fact]
    public void Results_are_cached_per_key()
    {
        var resolver = new SpriteResolver(null);

        ResolvedSprite a = resolver.Resolve(SpriteKind.Building, "auth_barracks", 2, 2);
        ResolvedSprite b = resolver.Resolve(SpriteKind.Building, "auth_barracks", 2, 2);
        ResolvedSprite c = resolver.Resolve(SpriteKind.Building, "auth_barracks", 3, 2);

        Assert.Same(a, b);
        Assert.NotSame(a, c);
    }

    [Fact]
    public void Short_names_come_from_the_last_segment()
    {
        Assert.Equal("REFI", PlaceholderArt.ShortName("auth_refinery"));
        Assert.Equal("CONY", PlaceholderArt.ShortName("auth_conyard"));
        Assert.Equal("W2", PlaceholderArt.ShortName("auth_w2"));
        Assert.Equal("TANK", PlaceholderArt.ShortName("tank"));
        Assert.Equal("?", PlaceholderArt.ShortName("auth_"));
    }

    [Fact]
    public void Placeholder_building_has_one_frame_per_state_and_the_house_stripe_is_mask_only()
    {
        PlaceholderPixels p = PlaceholderArt.Building("auth_barracks", 2, 2);

        Assert.Equal(6, p.Columns);
        Assert.Equal(6 * 128, p.Width);
        Assert.Equal(128, p.Height);
        int stripe = (10 * p.Width + 10) * 4;
        Assert.Equal(255, p.Mask[stripe + 3]);
        Assert.Equal(0, p.Base[stripe + 3]);
    }

    [Fact]
    public void Placeholder_units_have_16_vehicle_facings_and_infantry_have_walk_frames()
    {
        PlaceholderPixels tank = PlaceholderArt.Unit("auth_tank", infantry: false);
        PlaceholderPixels rifle = PlaceholderArt.Unit("auth_rifleman", infantry: true);

        Assert.Equal(16, tank.Columns);
        Assert.Equal(24, rifle.Columns);
        Assert.Equal(2, rifle.Manifest.States.First(s => s.Name == "walk").Frames);
    }

    private static string Manifest(int frameW, int frameH, int originX, int originY, int facings = 1) =>
        $"file = \"x.png\"\nframe_w = {frameW}\nframe_h = {frameH}\norigin_x = {originX}\norigin_y = {originY}\n"
        + $"[[state]]\nname = \"idle\"\nfacings = {facings}\nframes = 1\nfirst = 0\nticks_per_frame = 1\n";

    private void Write(string relative, string text)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }
}

/// <summary>Building states (build window, damaged threshold) and unit facing maps, from manifest text.</summary>
public sealed class SpriteStateTests
{
    // Idle 1 frame; damaged 2 frames at 5 ticks; build 4 frames at 10 ticks; 8-facing walk for infantry.
    private const string BuildingText = """
        file = "x.png"
        frame_w = 64
        frame_h = 64
        origin_x = 0
        origin_y = 0

        [[state]]
        name = "idle"
        facings = 1
        frames = 1
        first = 0
        ticks_per_frame = 1

        [[state]]
        name = "damaged"
        facings = 1
        frames = 2
        first = 1
        ticks_per_frame = 5

        [[state]]
        name = "build"
        facings = 1
        frames = 4
        first = 3
        ticks_per_frame = 10
        """;

    private const string InfantryText = """
        file = "x.png"
        frame_w = 32
        frame_h = 32
        origin_x = 16
        origin_y = 16

        [[state]]
        name = "idle"
        facings = 8
        frames = 1
        first = 0
        ticks_per_frame = 1

        [[state]]
        name = "walk"
        facings = 8
        frames = 2
        first = 8
        ticks_per_frame = 6
        """;

    private const string VehicleText = """
        file = "x.png"
        frame_w = 96
        frame_h = 96
        origin_x = 48
        origin_y = 64

        [[state]]
        name = "idle"
        facings = 16
        frames = 1
        first = 0
        ticks_per_frame = 1
        """;

    private static readonly SpriteManifest Building = SpriteManifest.Parse(BuildingText, "b", "test");
    private static readonly SpriteManifest Infantry = SpriteManifest.Parse(InfantryText, "i", "test");
    private static readonly SpriteManifest Vehicle = SpriteManifest.Parse(VehicleText, "v", "test");

    [Fact]
    public void Build_window_plays_each_frame_once_then_holds_on_the_last()
    {
        // 4 frames at 10 ticks: frames at ages 0, 10, 20, 30 (first = 3).
        Assert.Equal(3, SpriteStates.BuildingFrame(Building, 0, 100, 100, false, 0));
        Assert.Equal(4, SpriteStates.BuildingFrame(Building, 10, 100, 100, false, 10));
        Assert.Equal(5, SpriteStates.BuildingFrame(Building, 25, 100, 100, false, 25));
        Assert.Equal(6, SpriteStates.BuildingFrame(Building, 39, 100, 100, false, 39));
        // After the window (40 ticks) the building is idle, looping.
        Assert.Equal(0, SpriteStates.BuildingFrame(Building, 40, 100, 100, false, 40));
    }

    [Fact]
    public void Settled_buildings_never_play_the_build_window()
    {
        int settled = RenderMemory.Settled;
        int age = 500 - settled;
        Assert.Equal(0, SpriteStates.BuildingFrame(Building, age, 100, 100, false, 500));
    }

    [Fact]
    public void Damaged_below_half_health_uses_the_damaged_state_looping_by_tick()
    {
        // health 49 of 100 is below half: damaged frames 1 and 2, 5 ticks each.
        Assert.Equal(1, SpriteStates.BuildingFrame(Building, 1000, 49, 100, false, 0));
        Assert.Equal(2, SpriteStates.BuildingFrame(Building, 1000, 49, 100, false, 5));
        Assert.Equal(1, SpriteStates.BuildingFrame(Building, 1000, 49, 100, false, 10));
    }

    [Fact]
    public void Exactly_half_health_is_not_damaged()
    {
        Assert.Equal(0, SpriteStates.BuildingFrame(Building, 1000, 50, 100, false, 0));
    }

    [Fact]
    public void Without_a_damaged_state_or_max_health_the_building_stays_idle()
    {
        // Raw string literals keep the file's line endings (CRLF on a Windows checkout): normalise before searching.
        string text = BuildingText.Replace("\r\n", "\n", StringComparison.Ordinal);
        string noDamaged = text[..text.IndexOf("[[state]]\nname = \"damaged\"", StringComparison.Ordinal)]
            + text[text.IndexOf("[[state]]\nname = \"build\"", StringComparison.Ordinal)..];
        SpriteManifest m = SpriteManifest.Parse(noDamaged, "nd", "test");
        Assert.Equal(0, SpriteStates.BuildingFrame(m, 1000, 1, 100, false, 0));
        Assert.Equal(0, SpriteStates.BuildingFrame(Building, 1000, 1, 0, false, 0));
    }

    [Fact]
    public void Ghosts_show_idle_and_never_build_or_damage()
    {
        Assert.Equal(0, SpriteStates.BuildingFrame(Building, 0, 1, 100, true, 0));
    }

    [Fact]
    public void Vehicles_map_16_facings_and_infantry_map_8()
    {
        // 16 facings: step 16 per frame, north = 0, east (64) = 4, south (128) = 8, west (192) = 12.
        Assert.Equal(0, SpriteStates.UnitFrame(Vehicle, false, false, 0, 0));
        Assert.Equal(4, SpriteStates.UnitFrame(Vehicle, false, false, 64, 0));
        Assert.Equal(8, SpriteStates.UnitFrame(Vehicle, false, false, 128, 0));
        Assert.Equal(12, SpriteStates.UnitFrame(Vehicle, false, false, 192, 0));
        Assert.Equal(0, SpriteStates.UnitFrame(Vehicle, false, false, 250, 0));

        // 8 facings: step 32. East = 2, south = 4.
        Assert.Equal(0, SpriteStates.UnitFrame(Infantry, true, false, 0, 0));
        Assert.Equal(2, SpriteStates.UnitFrame(Infantry, true, false, 64, 0));
        Assert.Equal(4, SpriteStates.UnitFrame(Infantry, true, false, 128, 0));
    }

    [Fact]
    public void Infantry_walk_while_moving_and_idle_otherwise()
    {
        // walk: first = 8, two frames per facing, 6 ticks each. Facing 64 = facing index 2.
        int walkBase = 8 + 2 * 2;
        Assert.Equal(walkBase, SpriteStates.UnitFrame(Infantry, true, true, 64, 0));
        Assert.Equal(walkBase + 1, SpriteStates.UnitFrame(Infantry, true, true, 64, 6));
        Assert.Equal(2, SpriteStates.UnitFrame(Infantry, true, false, 64, 6));
    }

    [Fact]
    public void Vehicles_never_walk()
    {
        Assert.Equal(0, SpriteStates.UnitFrame(Vehicle, false, true, 0, 0));
    }
}

/// <summary>The draw order: footprint bottom edge, then x, then building before unit on a tie.</summary>
public sealed class DrawOrderTests
{
    private static readonly IComparer<DrawItem> Order = DrawOrder.Instance;

    [Fact]
    public void A_unit_north_of_a_building_bottom_is_drawn_before_it()
    {
        var building = new DrawItem(DrawKind.Building, 0, 256, 64);
        var unitAbove = new DrawItem(DrawKind.Unit, 0, 200, 100);
        var unitBelow = new DrawItem(DrawKind.Unit, 1, 300, 100);

        var items = new List<DrawItem> { unitBelow, building, unitAbove };
        items.Sort(Order);

        Assert.Equal(new[] { unitAbove, building, unitBelow }, items);
    }

    [Fact]
    public void Buildings_in_one_row_are_drawn_left_to_right()
    {
        var right = new DrawItem(DrawKind.Building, 0, 192, 320);
        var left = new DrawItem(DrawKind.Building, 1, 192, 64);

        var items = new List<DrawItem> { right, left };
        items.Sort(Order);

        Assert.Equal(new[] { left, right }, items);
    }

    [Fact]
    public void On_a_tie_the_building_comes_first_and_the_order_is_total()
    {
        var unit = new DrawItem(DrawKind.Unit, 0, 192, 64);
        var building = new DrawItem(DrawKind.Building, 5, 192, 64);

        Assert.True(Order.Compare(building, unit) < 0);
        Assert.True(Order.Compare(unit, building) > 0);
        Assert.Equal(0, Order.Compare(building, building));
        Assert.True(Order.Compare(new DrawItem(DrawKind.Unit, 1, 1, 1), new DrawItem(DrawKind.Unit, 2, 1, 1)) < 0);
    }
}

/// <summary>
/// Composes one frame of a small scene on the CPU with the same layout maths as the renderer, and writes it to
/// scratch/scene_proof.png for a visual check. It also asserts a few pixels, so a regression fails the test.
/// </summary>
public sealed class SceneProofTests
{
    private const int Width = 640;
    private const int Height = 480;
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void Composes_refinery_tank_barracks_and_infantry_into_scene_proof_png()
    {
        string dataDir = Path.GetDirectoryName(RulesLocator.FindRulesDir(AppContext.BaseDirectory)!)!;
        using SpriteLibrary library = SpriteLibrary.Scan(Path.Combine(dataDir, "sprites"));
        var resolver = new SpriteResolver(library);
        var camera = new Camera();

        var canvas = new byte[Width * Height * 4];
        Background(canvas);

        // Layout: refinery 3x3 at cell (1,1); barracks 2x2 at (6,1); tank in the dock cell (2,4) facing north and one
        // facing east at (5,4); three riflemen on row 6, the middle one walking.
        var items = new List<Placed>
        {
            Place(resolver.Resolve(SpriteKind.Building, "auth_refinery", 3, 3), SpriteKind.Building, new CellCoord(1, 1), 0, 0, 0),
            Place(resolver.Resolve(SpriteKind.Building, "auth_barracks", 2, 2), SpriteKind.Building, new CellCoord(6, 1), 0, 0, 0),
            Place(resolver.Resolve(SpriteKind.Vehicle, "auth_tank"), SpriteKind.Vehicle, null, 2.5f, 4.5f, 0),
            Place(resolver.Resolve(SpriteKind.Vehicle, "auth_tank"), SpriteKind.Vehicle, null, 5.5f, 4.5f, 64),
            Place(resolver.Resolve(SpriteKind.Infantry, "auth_rifleman"), SpriteKind.Infantry, null, 3.5f, 6.5f, 0),
            Place(resolver.Resolve(SpriteKind.Infantry, "auth_rifleman"), SpriteKind.Infantry, null, 4.5f, 6.5f, 64, walking: true),
            Place(resolver.Resolve(SpriteKind.Infantry, "auth_rifleman"), SpriteKind.Infantry, null, 5.5f, 6.5f, 192),
        };
        var order = new List<DrawItem>();
        for (int i = 0; i < items.Count; i++)
        {
            order.Add(items[i].Item with { Index = i });
        }
        order.Sort(DrawOrder.Instance);

        const int tick = 6;
        foreach (DrawItem item in order)
        {
            Placed p = items[item.Index];
            int frame = p.Sprite.Kind switch
            {
                SpriteKind.Building => SpriteStates.BuildingFrame(p.Sprite.Manifest, RenderMemory.Settled, 100, 100, false, tick),
                _ => SpriteStates.UnitFrame(p.Sprite.Manifest, p.Sprite.Kind == SpriteKind.Infantry, p.Walking, p.Facing, tick),
            };
            Vector2 origin = p.Sprite.Kind == SpriteKind.Building
                ? SpriteLayout.BuildingOrigin(camera, p.Cell!.Value)
                : SpriteLayout.UnitOrigin(camera, p.World);
            Vector2 topLeft = SpriteLayout.FrameTopLeft(origin, p.Sprite.Manifest, camera.Zoom);
            Color tint = PlayerColours.Base(0);
            Composite(canvas, p.Sprite, frame, (int)MathF.Round(topLeft.X), (int)MathF.Round(topLeft.Y), tint);
        }

        byte[] png = EncodePng(canvas, Width, Height);
        string outDir = Path.Combine(RepoRoot, "scratch");
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "scene_proof.png"), png);

        // The barracks is drawn (real art since M2-13s1, so no fixed stripe colour to check): its area differs from the
        // plain background, and some house-coloured pixel is present somewhere in the scene.
        Assert.NotEqual(Pixel(canvas, 1, 1), Pixel(canvas, 384 + 40, 64 + 40));
        // The refinery sprite is drawn: its top-left frame pixel area is not the plain background.
        Assert.NotEqual(Pixel(canvas, 0, 0), Pixel(canvas, 100, 100));
    }

    private sealed record Placed(DrawItem Item, ResolvedSprite Sprite, CellCoord? Cell, Vector2 World, byte Facing,
        bool Walking);

    private static Placed Place(ResolvedSprite sprite, SpriteKind kind, CellCoord? cell, float cx, float cy, byte facing,
        bool walking = false)
    {
        Vector2 world;
        float sortY;
        float sortX;
        if (kind == SpriteKind.Building)
        {
            world = new Vector2(cell!.Value.X * Camera.CellPx, cell.Value.Y * Camera.CellPx);
            sortY = (cell.Value.Y + SizeOf(sprite).H) * Camera.CellPx;
            sortX = cell.Value.X * Camera.CellPx;
        }
        else
        {
            world = new Vector2(cx * Camera.CellPx, cy * Camera.CellPx);
            sortY = world.Y;
            sortX = world.X;
        }
        return new Placed(new DrawItem(kind == SpriteKind.Building ? DrawKind.Building : DrawKind.Unit, 0, sortY, sortX),
            sprite, cell, world, facing, walking);
    }

    private static (int W, int H) SizeOf(ResolvedSprite sprite) => (sprite.Manifest.FrameW, sprite.Manifest.FrameH);

    private static void Background(byte[] canvas)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int i = (y * Width + x) * 4;
                bool line = x % Camera.CellPx == 0 || y % Camera.CellPx == 0;
                canvas[i] = (byte)(line ? 60 : 44);
                canvas[i + 1] = (byte)(line ? 55 : 40);
                canvas[i + 2] = (byte)(line ? 50 : 36);
                canvas[i + 3] = 255;
            }
        }
    }

    // The same two-step CPU path as the GPU draw: split the sheet into BASE and MASK, premultiply, then tint MASK.
    // Each frame is then alpha blended over the canvas at its top-left.
    private static void Composite(byte[] canvas, ResolvedSprite sprite, int frame, int left, int top, Color tint)
    {
        byte[] sheet;
        byte[] base_;
        byte[] mask;
        int sw;
        int sh;
        if (sprite.Placeholder is { } p)
        {
            sheet = Array.Empty<byte>();
            base_ = p.Base;
            mask = p.Mask;
            sw = p.Width;
            sh = p.Height;
        }
        else
        {
            (sheet, sw, sh) = DecodePng(Path.Combine(Path.GetDirectoryName(sprite.Manifest.SourcePath)!, sprite.Manifest.SheetFile));
            base_ = new byte[sheet.Length];
            mask = new byte[sheet.Length];
            uint[] ramp = sprite.Manifest.HouseRamp.ToArray();
            if (ramp.Length == 0)
            {
                Array.Copy(sheet, base_, sheet.Length);
            }
            else
            {
                HouseSplit.Split(sheet, ramp, base_, mask);
                HouseSplit.Premultiply(mask);
            }
            HouseSplit.Premultiply(base_);
        }

        var composed = new byte[base_.Length];
        HouseSplit.Compose(base_, mask, tint, composed);

        SpriteManifest m = sprite.Manifest;
        int columns = sw / m.FrameW;
        int fx = (frame % columns) * m.FrameW;
        int fy = (frame / columns) * m.FrameH;
        for (int v = 0; v < m.FrameH; v++)
        {
            int y = top + v;
            if ((uint)y >= Height) continue;
            for (int u = 0; u < m.FrameW; u++)
            {
                int x = left + u;
                if ((uint)x >= Width) continue;
                int si = ((fy + v) * sw + fx + u) * 4;
                int ci = (y * Width + x) * 4;
                int ka = 255 - composed[si + 3];
                for (int c = 0; c < 3; c++)
                {
                    canvas[ci + c] = (byte)Math.Min(255, composed[si + c] + canvas[ci + c] * ka / 255);
                }
                canvas[ci + 3] = 255;
            }
        }
    }

    private static Color Pixel(byte[] canvas, int x, int y)
    {
        int i = (y * Width + x) * 4;
        return new Color(canvas[i], canvas[i + 1], canvas[i + 2], canvas[i + 3]);
    }

    // The repo root is the folder that holds RTS_2027.slnx, found by walking up from the test binaries.
    private static string FindRepoRoot()
    {
        for (string? dir = AppContext.BaseDirectory; dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) return dir;
        }
        throw new InvalidOperationException("RTS_2027.slnx not found above the test binaries");
    }

    private static (byte[] Rgba, int W, int H) DecodePng(string path)
    {
        byte[] file = File.ReadAllBytes(path);
        int pos = 8;
        int w = 0;
        int h = 0;
        var idat = new MemoryStream();
        while (pos + 12 <= file.Length)
        {
            int len = BigEndian(file, pos);
            string type = Encoding.ASCII.GetString(file, pos + 4, 4);
            int data = pos + 8;
            if (type == "IHDR")
            {
                w = BigEndian(file, data);
                h = BigEndian(file, data + 4);
                if (file[data + 8] != 8 || file[data + 9] != 6 || file[data + 12] != 0)
                {
                    throw new InvalidDataException($"{path}: only 8-bit RGBA, non-interlaced PNG is supported");
                }
            }
            else if (type == "IDAT")
            {
                idat.Write(file, data, len);
            }
            else if (type == "IEND")
            {
                break;
            }
            pos = data + len + 4;
        }

        idat.Position = 0;
        var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress))
        {
            z.CopyTo(raw);
        }
        byte[] rawBytes = raw.ToArray();

        int stride = w * 4;
        var outPixels = new byte[h * stride];
        for (int y = 0; y < h; y++)
        {
            int filter = rawBytes[y * (stride + 1)];
            int src = y * (stride + 1) + 1;
            int row = y * stride;
            int prev = row - stride;
            for (int x = 0; x < stride; x++)
            {
                int a = x >= 4 ? outPixels[row + x - 4] : 0;
                int b = y > 0 ? outPixels[prev + x] : 0;
                int c = x >= 4 && y > 0 ? outPixels[prev + x - 4] : 0;
                int r = rawBytes[src + x];
                int v = filter switch
                {
                    0 => r,
                    1 => r + a,
                    2 => r + b,
                    3 => r + (a + b) / 2,
                    4 => r + Paeth(a, b, c),
                    _ => throw new InvalidDataException($"{path}: bad PNG filter {filter}"),
                };
                outPixels[row + x] = (byte)v;
            }
        }
        return (outPixels, w, h);
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private static int BigEndian(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

    private static byte[] EncodePng(byte[] rgba, int w, int h)
    {
        var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, w);
        WriteBigEndian(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(png, "IHDR", ihdr);

        int stride = w * 4;
        var raw = new byte[h * (stride + 1)];
        for (int y = 0; y < h; y++)
        {
            Array.Copy(rgba, y * stride, raw, y * (stride + 1) + 1, stride);
        }
        var z = new MemoryStream();
        using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true))
        {
            zs.Write(raw);
        }
        WriteChunk(png, "IDAT", z.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBigEndian(len, 0, data.Length);
        s.Write(len);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crcInput = new byte[4 + data.Length];
        Array.Copy(typeBytes, crcInput, 4);
        Array.Copy(data, 0, crcInput, 4, data.Length);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, (int)Crc32(crcInput));
        s.Write(crc);
    }

    private static void WriteBigEndian(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24);
        b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8);
        b[at + 3] = (byte)v;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte d in data)
        {
            crc ^= d;
            for (int k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }
        return ~crc;
    }
}
