// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;
using Microsoft.Xna.Framework;
using Rts.Game.Render;
using Rts.Game.Sprites;
using Xunit;

namespace Rts.Game.Tests.S45Anc;

/// <summary>V-05: the optional <c>[anchors]</c> table of a vehicle manifest (muzzle, dust) and how the renderer turns it into offsets.</summary>
public sealed class AnchorTests
{
    private static string Points(int facings, Func<int, (int, int)> f) =>
        "[" + string.Join(", ", Enumerable.Range(0, facings).Select(i => { (int x, int y) = f(i); return $"[{x}, {y}]"; })) + "]";

    private static string Text(int facings, string layer = "body", string extra = "", int pxPerCell = 64, string anchors = "")
    {
        var sb = new StringBuilder();
        sb.Append($"file = \"x.png\"\nframe_w = 100\nframe_h = 100\norigin_x = 50\norigin_y = 60\npx_per_cell = {pxPerCell}\n");
        if (layer == "turret") sb.Append("layer = \"turret\"\npivot_x = 0.0\npivot_y = 0.5\npivot_z = 1.0\n");
        sb.Append($"[[state]]\nname = \"idle\"\nfacings = {facings}\nframes = 1\nfirst = 0\nticks_per_frame = 1\n");
        sb.Append(extra).Append(anchors);
        return sb.ToString();
    }

    private static SpriteManifest Parse(string text, string id = "t") => SpriteManifest.Parse(text, id, id + ".sprite.toml");

    [Fact]
    public void Manifest_without_anchors_parses_and_has_none()
    {
        SpriteManifest m = Parse(Text(16));
        Assert.Null(m.Anchors);
        Assert.Null(AnchorOffsets.Muzzle(m, null, 0, null, 1.25f));
        Assert.Null(AnchorOffsets.Dust(m, 0, 1.25f));
    }

    [Fact]
    public void Manifest_with_anchors_parses_both_lists()
    {
        string table = $"[anchors]\nmuzzle = {Points(16, i => (i, -i))}\ndust = {Points(16, i => (-i, i * 2))}\n";
        SpriteManifest m = Parse(Text(16, anchors: table));
        Assert.NotNull(m.Anchors);
        Assert.True(m.Anchors!.HasMuzzle);
        Assert.True(m.Anchors.HasDust);
        Assert.Equal(16, m.Anchors.MuzzleCount);
        Assert.Equal((3, -3), m.Anchors.Muzzle(48));   // facing byte 48 = 3rd of 16 facings
        Assert.Equal((-3, 6), m.Anchors.Dust(48));
    }

    [Fact]
    public void A_table_with_only_one_key_leaves_the_other_empty()
    {
        SpriteManifest m = Parse(Text(8, anchors: $"[anchors]\ndust = {Points(8, _ => (0, 5))}\n"));
        Assert.True(m.Anchors!.HasDust);
        Assert.False(m.Anchors.HasMuzzle);
        Assert.Null(m.Anchors.Muzzle(0));
    }

    [Fact]
    public void Anchor_is_picked_per_facing_rounding_to_the_nearest_frame()
    {
        SpriteManifest m = Parse(Text(8, anchors: $"[anchors]\nmuzzle = {Points(8, i => (i * 10, -i * 10))}\n"));
        SpriteAnchors a = m.Anchors!;
        Assert.Equal((0, 0), a.Muzzle(0));
        Assert.Equal((0, 0), a.Muzzle(15));        // under half a step (16 of 32) rounds down
        Assert.Equal((10, -10), a.Muzzle(16));     // exactly half a step rounds up
        Assert.Equal((20, -20), a.Muzzle(64));     // east
        Assert.Equal((40, -40), a.Muzzle(128));    // south
        Assert.Equal((0, 0), a.Muzzle(250));       // wraps back to north
        Assert.Equal(m.FrameIndex("idle", 100, 0), a.Muzzle(100) is { } p ? p.Dx / 10 : -1);   // same facing index as the sheet frame
    }

    [Theory]
    [InlineData("[anchors]\nmuzzle = [[1, 2]]\n")]                   // wrong count for 16 facings
    [InlineData("[anchors]\nbogus = [[1, 2]]\n")]                     // unknown key
    [InlineData("[anchors]\nmuzzle = 5\n")]                           // not a list
    public void A_bad_anchors_table_is_an_error(string table)
    {
        Assert.Throws<InvalidDataException>(() => Parse(Text(16, anchors: table)));
    }

    [Fact]
    public void A_bad_pair_is_an_error()
    {
        string bad = "[" + string.Join(", ", Enumerable.Range(0, 8).Select(i => i == 3 ? "[1, 2, 3]" : "[0, 0]")) + "]";
        Assert.Throws<InvalidDataException>(() => Parse(Text(8, anchors: $"[anchors]\nmuzzle = {bad}\n")));
    }

    [Fact]
    public void A_fixed_gun_uses_the_body_muzzle_for_the_hull_facing_scaled_by_art_and_unit_scale()
    {
        SpriteManifest body = Parse(Text(16, pxPerCell: 80, anchors: $"[anchors]\nmuzzle = {Points(16, i => (i * 10, -100))}\n"));
        Vector2? at = AnchorOffsets.Muzzle(body, null, hullFacing: 64, turretFacing: 200, unitScale: 1.25f);   // the turret facing is ignored
        Assert.NotNull(at);
        Assert.Equal(40 * 0.8f * 1.25f, at!.Value.X, 3);
        Assert.Equal(-100 * 0.8f * 1.25f, at.Value.Y, 3);
    }

    [Fact]
    public void A_turret_muzzle_follows_the_turret_facing_and_sits_at_the_pivot_of_the_hull_facing()
    {
        SpriteManifest body = Parse(Text(16));
        SpriteManifest turret = Parse(Text(16, "turret", anchors: $"[anchors]\nmuzzle = {Points(16, i => (i, -50))}\n"), "t_turret");
        // hull faces north: the pivot (0, 0.5 cell forward) is 0.5 * 64 * sin 40 px above the origin; turret faces east (64 = index 4)
        Vector2? at = AnchorOffsets.Muzzle(body, turret, hullFacing: 0, turretFacing: 64, unitScale: 1f);
        Assert.NotNull(at);
        (int dx, int dy) = UnitLayerPlan.PivotOffset(turret.Pivot!.Value, 0, turret.PxPerCell);
        Assert.Equal(dx + 4, at!.Value.X, 3);
        Assert.Equal(dy - 50, at.Value.Y, 3);
        // the same turret facing with the hull turned east moves the pivot, not the turret's own anchor
        Vector2? turned = AnchorOffsets.Muzzle(body, turret, hullFacing: 64, turretFacing: 64, unitScale: 1f);
        (int tx, int ty) = UnitLayerPlan.PivotOffset(turret.Pivot!.Value, 90, turret.PxPerCell);
        Assert.Equal(tx + 4, turned!.Value.X, 3);
        Assert.Equal(ty - 50, turned.Value.Y, 3);
        // no turret facing: the hull facing is used for the turret too
        Vector2? same = AnchorOffsets.Muzzle(body, turret, hullFacing: 64, turretFacing: null, unitScale: 1f);
        Assert.Equal(turned, same);
    }

    [Fact]
    public void Without_a_turret_muzzle_the_body_muzzle_is_used_and_without_either_the_fallback_applies()
    {
        SpriteManifest body = Parse(Text(16, anchors: $"[anchors]\nmuzzle = {Points(16, _ => (7, 8))}\n"));
        SpriteManifest turretNoAnchors = Parse(Text(16, "turret"), "t_turret");
        Assert.Equal(new Vector2(7, 8), AnchorOffsets.Muzzle(body, turretNoAnchors, 0, 0, 1f));
        SpriteManifest bare = Parse(Text(16));
        Assert.Null(AnchorOffsets.Muzzle(bare, turretNoAnchors, 0, 0, 1f));   // today's offset (the flash stays on the shell)
    }

    [Fact]
    public void Dust_anchor_is_picked_per_hull_facing()
    {
        SpriteManifest m = Parse(Text(16, anchors: $"[anchors]\ndust = {Points(16, i => (i, 20))}\n"));
        Assert.Equal(new Vector2(0, 20), AnchorOffsets.Dust(m, 0, 1f));
        Assert.Equal(new Vector2(8, 20), AnchorOffsets.Dust(m, 128, 1f));
        Assert.Equal(new Vector2(8 * 1.25f, 20 * 1.25f), AnchorOffsets.Dust(m, 128, 1.25f));
    }

    [Fact]
    public void Shipped_vehicle_manifests_parse_and_their_anchors_point_the_right_way()
    {
        string dir = SpriteManifestTests.RepoFile("data", "sprites", "vehicles");
        int withAnchors = 0;
        foreach (string file in Directory.GetFiles(dir, "*.sprite.toml"))
        {
            SpriteManifest m = SpriteManifest.Load(file);
            if (m.Anchors is not null) withAnchors++;
        }
        Assert.True(withAnchors > 30);

        SpriteManifest tank = SpriteManifest.Load(Path.Combine(dir, "auth_tank.sprite.toml"));
        (int Dx, int Dy) north = tank.Anchors!.Muzzle(0)!.Value, east = tank.Anchors.Muzzle(64)!.Value;
        (int Dx, int Dy) south = tank.Anchors.Muzzle(128)!.Value, west = tank.Anchors.Muzzle(192)!.Value;
        Assert.True(north.Dy < -40 && Math.Abs(north.Dx) <= 1);   // north: straight up the screen
        Assert.True(east.Dx > 30);                                // east: right of the origin
        Assert.True(west.Dx < -30);                               // west: left (clockwise facings)
        Assert.True(south.Dy > north.Dy);                         // south: lower on screen than north
        (int Dx, int Dy) dustNorth = tank.Anchors.Dust(0)!.Value;
        Assert.True(dustNorth.Dy > 0);                            // the dust is behind a north-facing tank: below the origin
    }
}
