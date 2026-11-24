// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Tests.Audio;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Render;

/// <summary>A43 FOLLOWUP-11 / FOLLOWUP-12: the render path allocates nothing per frame once warm, and view culling drops only what is off screen.</summary>
public sealed class RenderPerfTests
{
    private const string Body = "file = \"t.png\"\nframe_w = 32\nframe_h = 32\norigin_x = 16\norigin_y = 24\n"
        + "[[state]]\nname = \"idle\"\nfacings = 16\nframes = 1\nfirst = 0\nticks_per_frame = 1\n";

    private static string Companion(string layer, string extra) => "file = \"t.png\"\nframe_w = 32\nframe_h = 32\norigin_x = 16\norigin_y = 24\n"
        + $"layer = \"{layer}\"\n{extra}\n[[state]]\nname = \"idle\"\nfacings = 16\nframes = 1\nfirst = 0\nticks_per_frame = 1\n";

    private static long Allocated(Action action)
    {
        action();   // warm: JIT, buffers at their working size
        action();
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void The_struct_layer_plan_equals_the_list_plan_for_every_input()
    {
        SpriteManifest body = SpriteManifest.Parse(Body, "x", "p");
        SpriteManifest turret = SpriteManifest.Parse(Companion("turret", "pivot_x = 0.0\npivot_y = 0.2794\npivot_z = 0.6"), "x_turret", "p");
        SpriteManifest shadow = SpriteManifest.Parse(Companion("shadow", ""), "x_shadow", "p");
        foreach ((SpriteManifest? t, SpriteManifest? s) in new[] { ((SpriteManifest?)null, (SpriteManifest?)null), (turret, null), (null, shadow), (turret, shadow) })
        foreach (bool moving in new[] { false, true })
        foreach (bool diving in new[] { false, true })
        {
            for (int facing = 0; facing < 256; facing += 7)
            {
                UnitLayers planned = UnitLayerPlan.Plan(body, false, moving, (byte)facing, (byte)(255 - facing), diving, facing * 3, t, s, facing * 11L);
                IReadOnlyList<UnitLayer> list = UnitLayerPlan.For(body, false, moving, (byte)facing, (byte)(255 - facing), diving, facing * 3, t, s, facing * 11L);
                Assert.Equal(list.Count, planned.Count);
                int i = 0;
                foreach (UnitLayer layer in planned) Assert.Equal(list[i++], layer);
                Assert.Equal(list.Count, i);
            }
        }
    }

    [Fact]
    public void Planning_unit_layers_allocates_nothing()
    {
        SpriteManifest body = SpriteManifest.Parse(Body, "x", "p");
        SpriteManifest turret = SpriteManifest.Parse(Companion("turret", "pivot_x = 0.0\npivot_y = 0.2794\npivot_z = 0.6"), "x_turret", "p");
        SpriteManifest shadow = SpriteManifest.Parse(Companion("shadow", ""), "x_shadow", "p");
        int sum = 0;

        long bytes = Allocated(() =>
        {
            for (int i = 0; i < 1000; i++)
                foreach (UnitLayer layer in UnitLayerPlan.Plan(body, false, true, (byte)i, null, false, i, turret, shadow, i * 5L)) sum += layer.Frame;
        });

        Assert.Equal(0, bytes);
        Assert.True(sum >= 0);
    }

    [Fact]
    public void Render_memory_swaps_its_buffers_without_losing_state_and_allocates_nothing_when_warm()
    {
        var memory = new RenderMemory();
        UnitView a = AudioTestSupport.Unit(1, 0, 3, 3);
        UnitView gone = AudioTestSupport.Unit(2, 0, 5, 5);
        BuildingView b = AudioTestSupport.Building(1, 0, 8, 8);
        memory.Observe(AudioTestSupport.Snap(1, new[] { a, gone }, new[] { b }));
        UnitView a2 = a with { Position = new LeptonCoord(a.Position.X + 40, a.Position.Y) };
        memory.Observe(AudioTestSupport.Snap(2, new[] { a2 }, new[] { b }));
        UnitView a3 = a2 with { Position = new LeptonCoord(a2.Position.X + 40, a2.Position.Y) };
        memory.Observe(AudioTestSupport.Snap(3, new[] { a3 }, new[] { b }));

        Assert.Equal(80, memory.Travel(UnitKey.Of(a)));
        Assert.True(memory.IsMoving(UnitKey.Of(a)));
        Assert.Equal(0, memory.Travel(UnitKey.Of(gone)));   // forgotten when it left the snapshot
        Assert.False(memory.IsMoving(UnitKey.Of(gone)));
        Assert.True(memory.BuildingAge(b.Id, 3) > 1_000_000);   // the first-sighting building stays settled across swaps

        var units = new UnitView[300];
        for (int i = 0; i < units.Length; i++) units[i] = AudioTestSupport.Unit(i + 1, 0, i % 50, i / 50);
        var blds = new BuildingView[40];
        for (int i = 0; i < blds.Length; i++) blds[i] = AudioTestSupport.Building(i + 1, 0, i, 60);
        var snaps = new SimSnapshot[8];
        for (int i = 0; i < snaps.Length; i++)
        {
            UnitView[] moved = new UnitView[units.Length];
            for (int u = 0; u < units.Length; u++) moved[u] = units[u] with { Position = new LeptonCoord(units[u].Position.X + i * 10, units[u].Position.Y) };
            snaps[i] = AudioTestSupport.Snap(10 + i, moved, blds, w: 64, h: 64);
        }

        int next = 0;
        long bytes = Allocated(() => { for (int i = 0; i < 2; i++) memory.Observe(snaps[next++ % snaps.Length]); });
        Assert.True(bytes == 0, $"RenderMemory.Observe allocated {bytes} bytes");
    }

    [Fact]
    public void Dust_emitter_allocates_nothing_when_warm()
    {
        var emitter = new DustEmitter();
        var field = new PuffField();
        var units = new UnitView[300];
        for (int i = 0; i < units.Length; i++) units[i] = AudioTestSupport.Unit(i + 1, 0, i % 50, i / 50);
        var snaps = new SimSnapshot[6];
        for (int i = 0; i < snaps.Length; i++) snaps[i] = AudioTestSupport.Snap(10 + i, units, w: 64, h: 64);
        int next = 0;

        long bytes = Allocated(() => { for (int i = 0; i < 2; i++) emitter.Observe(snaps[next++ % snaps.Length], null, field, 0x808080); });

        Assert.True(bytes == 0, $"DustEmitter.Observe allocated {bytes} bytes");
    }

    [Fact]
    public void The_interpolator_matches_a_fresh_computation_and_allocates_nothing_per_frame()
    {
        UnitView a = AudioTestSupport.Unit(1, 0, 2, 2), c = AudioTestSupport.Unit(2, 0, 4, 4);
        SimSnapshot s1 = AudioTestSupport.Snap(1, new[] { a, c });
        UnitView a2 = a with { Position = new LeptonCoord(a.Position.X + 256, a.Position.Y) };
        UnitView fresh = AudioTestSupport.Unit(3, 0, 9, 9);
        SimSnapshot s2 = AudioTestSupport.Snap(2, new[] { a2, fresh });
        UnitView a3 = a2 with { Position = new LeptonCoord(a2.Position.X + 256, a2.Position.Y) };
        SimSnapshot s3 = AudioTestSupport.Snap(3, new[] { a3, fresh });
        var outList = new List<RenderUnit>();

        UnitInterpolator.Build(s2, s1, 0.5f, outList);
        Assert.Equal(Vector2.Lerp(Camera.WorldFromLepton(a.Position), Camera.WorldFromLepton(a2.Position), 0.5f), outList[0].World);
        Assert.Equal(Camera.WorldFromLepton(fresh.Position), outList[1].World);   // no match: drawn where it is
        UnitInterpolator.Build(s3, s2, 0.25f, outList);   // a new previous snapshot: the map must be refilled
        Assert.Equal(Vector2.Lerp(Camera.WorldFromLepton(a2.Position), Camera.WorldFromLepton(a3.Position), 0.25f), outList[0].World);
        Assert.Equal(Camera.WorldFromLepton(fresh.Position), outList[1].World);   // matched now, and has not moved
        UnitInterpolator.Build(s3, s3, 0.9f, outList);   // same snapshot twice: positions as they are
        Assert.Equal(Camera.WorldFromLepton(a3.Position), outList[0].World);

        var many = new UnitView[400];
        for (int i = 0; i < many.Length; i++) many[i] = AudioTestSupport.Unit(i + 1, 0, i % 60, i / 60);
        SimSnapshot big1 = AudioTestSupport.Snap(5, many, w: 64, h: 64), big2 = AudioTestSupport.Snap(6, many, w: 64, h: 64);
        float alpha = 0f;
        long bytes = Allocated(() => { alpha = (alpha + 0.1f) % 1f; UnitInterpolator.Build(big2, big1, alpha, outList); });
        Assert.True(bytes == 0, $"UnitInterpolator.Build allocated {bytes} bytes per frame");
        Assert.Equal(many.Length, outList.Count);
    }

    [Fact]
    public void Grout_rects_are_built_once()
    {
        Assert.Same(GroundOverlay.GroutRects(), GroundOverlay.GroutRects());
        Assert.Equal(6, GroundOverlay.GroutRects().Count);   // three courses: 1 + (joint line + 2) + (joint line + 1)
    }

    // A fixed reach for every sprite; the test picks it larger than anything it places on the screen edge by accident.
    private sealed class FixedReach : ISpriteExtents
    {
        public float Unit = 100f;
        public float Building = 150f;
        public float UnitReach(UnitView unit) => Unit;
        public float BuildingReach(BuildingView building) => Building;
    }

    private static (List<BuildingView> Buildings, List<RenderUnit> Units) Scatter()
    {
        var buildings = new List<BuildingView>();
        var units = new List<RenderUnit>();
        for (int i = 0; i < 400; i++)
        {
            int cx = i * 37 % 120, cy = i * 53 % 120;
            if (i % 5 == 0) buildings.Add(AudioTestSupport.Building(i + 1, (byte)(i % 3), cx, cy));
            UnitView u = AudioTestSupport.Unit(i + 1, (byte)(i % 3), cx, cy);
            units.Add(new RenderUnit(u, Camera.WorldFromLepton(u.Position)));
        }
        return (buildings, units);
    }

    // The true on-screen test: does a unit or building touch the screen at all, given its reach.
    private static bool Touches(DrawItem item, List<BuildingView> buildings, float reachU, float reachB, Camera camera, Viewport vp)
    {
        float l = camera.Position.X, t = camera.Position.Y, r = l + vp.Width / camera.Zoom, b = t + vp.Height / camera.Zoom;
        if (item.Kind == DrawKind.Unit) return item.SortX + reachU >= l && item.SortX - reachU <= r && item.SortY + reachU >= t && item.SortY - reachU <= b;
        BuildingView bv = buildings[item.Index];
        float bl = bv.Cell.X * Camera.CellPx, bt = bv.Cell.Y * Camera.CellPx;
        return bl + bv.FootprintW * Camera.CellPx + reachB >= l && bl - reachB <= r && bt + bv.FootprintH * Camera.CellPx + reachB >= t && bt - reachB <= b;
    }

    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(1500, 900, 1)]
    [InlineData(3000, 2500, 2)]
    [InlineData(2200, 1200, 0)]
    public void A_culled_draw_list_equals_the_unculled_one_for_on_screen_items(int px, int py, int zoomSteps)
    {
        (List<BuildingView> buildings, List<RenderUnit> units) = Scatter();
        var camera = new Camera();
        camera.ZoomBy(zoomSteps == 0 ? -1 : zoomSteps == 2 ? 1 : 0);
        camera.MoveTo(new Vector2(px, py));
        var vp = new Viewport(0, 0, 1280, 720);
        var reach = new FixedReach();

        var all = new List<DrawItem>();
        var culled = new List<DrawItem>();
        SpriteCull.Collect(all, buildings, units, null, camera, vp);
        SpriteCull.Collect(culled, buildings, units, reach, camera, vp);
        all.Sort(DrawOrder.Instance);
        culled.Sort(DrawOrder.Instance);
        var viaStatic = new List<DrawItem>(culled.Count);
        SpriteCull.Collect(viaStatic, buildings, units, reach, camera, vp);
        DrawOrder.Sort(viaStatic);
        Assert.Equal(culled, viaStatic);   // the allocation-free sort gives the comparer's order

        List<DrawItem> expected = all.Where(i => Touches(i, buildings, reach.Unit, reach.Building, camera, vp)).ToList();
        Assert.Equal(expected, culled);   // same items, same draw order
        Assert.Equal(buildings.Count + units.Count, all.Count);
        Assert.True(culled.Count < all.Count, "something off screen must be culled");
        Assert.NotEmpty(culled);
        // Every item a frame at exactly the real reach could show is in the list: nothing within the view is lost.
        Assert.All(all.Where(i => Touches(i, buildings, 0f, 0f, camera, vp)), i => Assert.Contains(i, culled));
    }

    [Fact]
    public void Culling_keeps_a_tall_sprite_whose_anchor_is_just_off_screen()
    {
        var camera = new Camera();
        camera.MoveTo(Vector2.Zero);
        var vp = new Viewport(0, 0, 1280, 720);
        UnitView below = AudioTestSupport.Unit(1, 0, 5, 12);   // ground point a little under the screen bottom (720 px = 11.25 cells)
        var units = new List<RenderUnit> { new(below, Camera.WorldFromLepton(below.Position)) };
        var items = new List<DrawItem>();

        SpriteCull.Collect(items, new List<BuildingView>(), units, new FixedReach { Unit = 120 }, camera, vp);
        Assert.Single(items);   // 12.5 cells = 800 px, 80 px below the edge, within the 120 px reach
        items.Clear();
        SpriteCull.Collect(items, new List<BuildingView>(), units, new FixedReach { Unit = 60 }, camera, vp);
        Assert.Empty(items);
    }

    [Fact]
    public void Collecting_the_draw_list_allocates_nothing_when_warm()
    {
        (List<BuildingView> buildings, List<RenderUnit> units) = Scatter();
        var camera = new Camera();
        camera.MoveTo(new Vector2(1500, 900));
        var vp = new Viewport(0, 0, 1280, 720);
        var reach = new FixedReach();
        var items = new List<DrawItem>();

        long bytes = Allocated(() => { items.Clear(); SpriteCull.Collect(items, buildings, units, reach, camera, vp); DrawOrder.Sort(items); });

        Assert.True(bytes == 0, $"draw list allocated {bytes} bytes");
    }
}
