// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Ui;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.M3;

/// <summary>M3-08: the pure parts of the radiation / hardened / Blackout overlay, unit ghosts and the minimap jam.</summary>
public sealed class GroundAndUnitVisualTests
{
    private static SimSnapshot Snapshot(int w, int h, byte[] ground, int tick = 10) =>
        new(tick, w, h, Array.Empty<UnitView>(), new LandType[w * h], 0UL, Array.Empty<BuildingView>(), 0, Array.Empty<byte>())
        {
            Ground = ground,
        };

    private static UnitView Unit(bool underground = false, bool stealthed = false) =>
        new(new EntityId(1, 1), UnitKind.Vehicle, 0, 0, new LeptonCoord(0, 0), 0, 100) { Underground = underground, Stealthed = stealthed };

    [Fact]
    public void Radiation_tint_follows_the_level_and_is_zero_for_clean_ground()
    {
        Assert.Equal(0, GroundOverlay.RadiationAlpha(0, 5));
        for (int tick = 0; tick < 24; tick++)
        {
            for (int level = 1; level < 15; level++)
                Assert.True(GroundOverlay.RadiationAlpha(level + 1, tick) > GroundOverlay.RadiationAlpha(level, tick) - 1);
        }

        Assert.True(GroundOverlay.RadiationAlpha(15, 8) > GroundOverlay.RadiationAlpha(1, 8) + 60);
        Assert.Equal(GroundOverlay.RadiationAlpha(7, 33), GroundOverlay.RadiationAlpha(7, 33));
    }

    [Fact]
    public void Ground_byte_fields_are_read_from_the_contract_bits()
    {
        byte g = (byte)(GroundLayers.Hardened | GroundLayers.Blackout | 9);
        Assert.Equal(9, GroundOverlay.RadiationLevel(g));
    }

    [Fact]
    public void Overlay_has_data_only_when_the_ground_array_covers_the_map()
    {
        Assert.False(GroundOverlay.HasData(Snapshot(4, 4, Array.Empty<byte>())));
        Assert.False(GroundOverlay.HasData(Snapshot(4, 4, new byte[5])));
        Assert.True(GroundOverlay.HasData(Snapshot(4, 4, new byte[16])));
    }

    [Fact]
    public void Grout_stays_inside_one_cell_and_is_thin()
    {
        IReadOnlyList<Microsoft.Xna.Framework.Rectangle> rects = GroundOverlay.GroutRects();
        Assert.NotEmpty(rects);
        foreach (var r in rects)
        {
            Assert.True(r.X >= 0 && r.Y >= 0 && r.Right <= Camera.CellPx && r.Bottom <= Camera.CellPx);
            Assert.True(Math.Min(r.Width, r.Height) <= 2);
        }
    }

    [Fact]
    public void Underground_units_are_faint_stealthed_units_semi_transparent_and_others_opaque()
    {
        Assert.Equal(1f, UnitVisuals.Alpha(Unit()));
        Assert.Equal(UnitVisuals.StealthAlpha, UnitVisuals.Alpha(Unit(stealthed: true)));
        Assert.Equal(UnitVisuals.UndergroundAlpha, UnitVisuals.Alpha(Unit(underground: true)));
        Assert.Equal(UnitVisuals.UndergroundAlpha, UnitVisuals.Alpha(Unit(underground: true, stealthed: true)));
        Assert.True(UnitVisuals.UndergroundAlpha < UnitVisuals.StealthAlpha && UnitVisuals.StealthAlpha < 1f);
        Assert.True(UnitVisuals.ShowTunnelMarker(Unit(underground: true)));
        Assert.False(UnitVisuals.ShowTunnelMarker(Unit(stealthed: true)));
    }

    [Fact]
    public void Jam_fills_only_jammed_cells_with_opaque_static()
    {
        var ground = new byte[9];
        ground[4] = GroundLayers.Jammed;
        ground[0] = GroundLayers.Blackout;   // not a jam
        SimSnapshot s = Snapshot(3, 3, ground);
        var pixels = new Microsoft.Xna.Framework.Color[9];

        Assert.True(MinimapJam.Fill(pixels, s));

        for (int i = 0; i < 9; i++)
        {
            if (i == 4) Assert.Equal(255, pixels[i].A);
            else Assert.Equal(0, pixels[i].A);
        }
    }

    [Fact]
    public void Jam_with_no_jammed_cell_or_no_ground_data_reports_false()
    {
        var pixels = new Microsoft.Xna.Framework.Color[4];
        Assert.False(MinimapJam.Fill(pixels, Snapshot(2, 2, new byte[4])));
        Assert.False(MinimapJam.Fill(pixels, Snapshot(2, 2, Array.Empty<byte>())));
        Assert.False(MinimapJam.IsJammed(Snapshot(2, 2, Array.Empty<byte>()), 0, 0));
        Assert.False(MinimapJam.IsJammed(Snapshot(2, 2, new byte[4]), 5, 5));
    }

    [Fact]
    public void Jam_static_flickers_over_time_and_is_a_pure_function()
    {
        Assert.Equal(MinimapJam.Noise(3, 4, 10), MinimapJam.Noise(3, 4, 10));
        Assert.Equal(MinimapJam.Noise(3, 4, 10), MinimapJam.Noise(3, 4, 11));   // same flicker step
        int changed = 0;
        for (int tick = 0; tick < 40; tick += MinimapJam.FlickerTicks)
            if (MinimapJam.Noise(3, 4, tick) != MinimapJam.Noise(3, 4, tick + MinimapJam.FlickerTicks)) changed++;
        Assert.True(changed > 10);
    }
}
