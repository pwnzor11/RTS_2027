// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Screens.Editor;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Game.Tests.Editor;

/// <summary>The map editor asks for the same sprite keys (prop kind, variant, atlas frame, cell) and decal bake as the game view for the same map.</summary>
public class EditorSpriteTests
{
    [Fact]
    public void Editor_map_with_a_tree_and_a_decal_asks_for_the_same_sprite_keys_as_the_game_view()
    {
        var doc = EditorDocument.Create("sprites", "rust_belt", 48, 48);
        var session = new EditorSession(doc);
        session.Tool = EditorTool.Props; session.Prop = OverlayKind.Tree; session.PropVariant = 1; session.BrushSize = 1;
        Assert.True(session.Click(10, 10));
        session.Tool = EditorTool.Decal; session.DecalKind = 1; session.BrushSize = 3;
        Assert.True(session.Click(20, 20));
        MapData map = doc.ToMapData();

        // The game view for the same map, built the way DecorLayer.Rebuild builds it (same palette, seed, planners and atlas chain).
        TerrainPalette palette = TerrainPalette.ForMap(map);
        ulong seed = MapHasher.Compute(map);
        Assert.NotEmpty(palette.Decor.Decals);
        DecorBake.Result gameDecals = DecorBake.Bake(map, palette, seed);
        Assert.True(gameDecals.Count > 0, "the decal stamped in the editor is baked by the game view");
        PropInstance[] props = PropPlanner.Plan(map, palette, seed);
        PropInstance[] blocking = PropPlanner.PlanBlocking(map);
        IPropAtlas atlas = PropAtlases.Create(palette, TerrainPalette.TheaterIdFor(map, null));
        var gameSprites = new List<EditorSprites.Sprite>();
        foreach (PropInstance p in props) gameSprites.Add(new EditorSprites.Sprite(p, atlas.Frame(p.Kind, p.Variant), false));
        foreach (PropInstance p in blocking) gameSprites.Add(new EditorSprites.Sprite(p, atlas.Frame(p.Kind, p.Variant), true));

        // The editor's plan for its document.
        EditorSprites.Plan plan = EditorSprites.PlanFor(map);

        Assert.Equal(gameSprites, plan.Sprites);
        Assert.Equal(gameDecals.Count, plan.Decals?.Count ?? 0);
        Assert.Equal(gameDecals.Rgba, plan.Decals?.Rgba);
        Assert.Contains(plan.Sprites, s => s.Blocking && s.Prop.Kind == PropKind.DeadTree && s.Prop.CellX == 10 && s.Prop.CellY == 10 && s.Prop.Variant == 1);
    }
}
