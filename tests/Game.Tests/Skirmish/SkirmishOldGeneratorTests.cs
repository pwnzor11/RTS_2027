// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Render;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Skirmish;

/// <summary>M12-05 (D-180 D4): the generator was replaced, so a skirmish save made on a generated map with the old generator is refused with a
/// friendly message instead of loading a different map.</summary>
public sealed class SkirmishOldGeneratorTests
{
    private static SkirmishSettings Settings(int layout) =>
        new(new MapChoice(null, new MapGenParams(Biome.RustBelt, MapSize.Small, 2, 7, null, OceanEdges.None, layout)), SkirmishSettings.Default.Slots, 7);

    private static GameSaveHeader RoundTrip(GameSaveHeader h)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) h.Write(w);
        ms.Position = 0;
        return GameSaveHeader.Read(new BinaryReader(ms), h.Version);
    }

    private static GameSaveHeader Header(SkirmishSettings s, int version) => new()
    {
        Mode = SaveMode.Skirmish, MapId = "gen:rust_belt:0:2:7", MapName = "x", RulesHash = 1, Skirmish = s, Build = "b", Version = version,
    };

    [Fact]
    public void The_layout_round_trips_in_a_version_6_header_and_an_older_header_reads_as_layout_0()
    {
        Assert.Equal(1, RoundTrip(Header(Settings(1), GameSaveHeader.CurrentVersion)).Skirmish!.Map.Generated!.Layout);
        Assert.Equal(0, RoundTrip(Header(Settings(1), 5)).Skirmish!.Map.Generated!.Layout);   // version 5 carries no layout byte
    }

    [Fact]
    public void An_old_layout_cannot_be_rebuilt_and_says_so_kindly()
    {
        SkirmishSettings old = Settings(0);
        var e = Assert.Throws<SaveLoadException>(() => GameLoader.ResolveMapWithMeta("gen:rust_belt:0:2:7", Path.GetTempPath(), old));
        Assert.Equal("This save was made with an older map generator and can't be loaded.", e.Message);
        // the same save with the current layout rebuilds
        Assert.NotNull(GameLoader.ResolveMapWithMeta("gen:rust_belt:0:2:7", Path.GetTempPath(), Settings(1)).Map);
        // and a gen: id with no settings (nothing says which generator) is read as the current one only when it has settings; without them it is current
        Assert.NotNull(GameLoader.ResolveMapWithMeta("gen:rust_belt:0:2:7", Path.GetTempPath(), null).Map);
    }

    [Fact]
    public void Blocking_props_sort_with_units_by_the_bottom_edge_of_their_cell()
    {
        var items = new List<DrawItem>
        {
            new(DrawKind.Unit, 0, 5 * 64 + 40, 3 * 64),     // a unit in the row below the prop
            new(DrawKind.Prop, 0, 5 * 64, 3 * 64),          // prop on row 4: bottom edge at 5 * 64
            new(DrawKind.Unit, 1, 5 * 64 - 10, 3 * 64),     // a unit standing at the prop's feet row: north of the bottom edge
        };
        items.Sort(DrawOrder.Instance);
        Assert.Equal(new[] { (DrawKind.Unit, 1), (DrawKind.Prop, 0), (DrawKind.Unit, 0) }, items.Select(i => (i.Kind, i.Index)).ToArray());
    }

    [Fact]
    public void Every_blocking_overlay_kind_has_a_sprite_kind_and_art()
    {
        TerrainPalette pal = TerrainPalette.Load("rustbelt");
        foreach (OverlayKind k in new[] { OverlayKind.Tree, OverlayKind.Wreck, OverlayKind.Block, OverlayKind.Pylon, OverlayKind.Snag })
            Assert.Contains(PropArt.Render(PropPlanner.KindOf(k), 1, pal), b => b > 0);
    }
}
