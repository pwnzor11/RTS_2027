// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Game.Render;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.M12_16;

/// <summary>M12-16 step 4 (D-264): what the renderer and the audio do with <see cref="FxKind.PropDestroyed"/> and with a cleared overlay.</summary>
public sealed class PropDestroyedRenderTests
{
    private static readonly LeptonCoord At = LeptonCoord.CenterOf(new CellCoord(8, 8));

    private static SimSnapshot Snap(int tick, params FxEvent[] fx) =>
        new(tick, 16, 16, new List<UnitView>(), new LandType[256], 0, new List<BuildingView>(), -1, Array.Empty<byte>(), fx.ToList());

    [Fact]
    public void ThePropEvent_IsAppendedAfterTheOldOnes()
    {
        Assert.Equal(5, (int)FxKind.GarrisonExit);
        Assert.Equal(6, (int)FxKind.PropDestroyed);
    }

    [Fact]
    public void AFallenProp_IsASmallExplosionSound_AtItsCell()
    {
        SimSnapshot a = Audio.AudioTestSupport.Snap(1);
        SimSnapshot b = Audio.AudioTestSupport.Snap(2, fx: new[] { new FxEvent(FxKind.PropDestroyed, At, 1) });
        List<AudioEvent> e = Derive(new AudioEventDeriver(), Frame(a, b));
        AudioEvent only = Assert.Single(e);
        Assert.Equal(AudioEventKind.ExplosionSmall, only.Kind);
        Assert.Equal(At, only.Pos);
    }

    [Fact]
    public void AFallenProp_SpawnsNoExplosionSprite()
    {
        Assert.Empty(ExplosionPlan.For(new FxEvent(FxKind.PropDestroyed, At, 1)));
        Assert.Single(ExplosionPlan.For(new FxEvent(FxKind.Detonation, At, 1)));   // the others are unchanged
    }

    [Fact]
    public void AFallenProp_RaisesAFewDustPuffs_OncePerTick()
    {
        var dust = new PropDust();
        var field = new PuffField();
        dust.Observe(Snap(1), field, 0xAA9977);                                        // first sight of the game: silent
        dust.Observe(Snap(2, new FxEvent(FxKind.PropDestroyed, At, 1)), field, 0xAA9977);
        Assert.Equal(PropDust.PuffsPerProp, field.Items.Count);
        Assert.All(field.Items, p => Assert.Equal(0xAA9977u, p.Rgb));
        dust.Observe(Snap(2, new FxEvent(FxKind.PropDestroyed, At, 1)), field, 0xAA9977);   // the same snapshot drawn again: nothing new
        Assert.Equal(PropDust.PuffsPerProp, field.Items.Count);
        dust.Observe(Snap(3, new FxEvent(FxKind.Detonation, At, 1), new FxEvent(FxKind.UnitDestroyed, At, 1)), field, 0xAA9977);
        Assert.Equal(PropDust.PuffsPerProp, field.Items.Count);                        // only prop events make dust
        var centre = Camera.WorldFromLepton(At);
        Assert.All(field.Items, p => Assert.True(Math.Abs(p.X - centre.X) < Camera.CellPx && Math.Abs(p.Y - centre.Y) < Camera.CellPx));
    }

    [Fact]
    public void ARestartOrAJumpBack_MakesNoDust()
    {
        var dust = new PropDust();
        var field = new PuffField();
        dust.Observe(Snap(50), field, 0x808080);
        dust.Observe(Snap(10, new FxEvent(FxKind.PropDestroyed, At, 1)), field, 0x808080);
        Assert.Empty(field.Items);
    }

    [Fact]
    public void ABlockingSprite_IsDrawnWhileTheOverlayStands_AndStopsWhenTheSimClearsIt()
    {
        MapData flat = MapData.CreateFlat(16, 16, "props");
        var ovl = new OverlayKind[256];
        ovl[8 * 16 + 8] = OverlayKind.Tree;
        ovl[8 * 16 + 9] = OverlayKind.Block;
        var map = new MapData("props", "rust_belt", 16, 16, new ushort[256], new LandType[256], new byte[256], ovl, new byte[256], flat.StartPositions);
        PropInstance[] plan = PropPlanner.PlanBlocking(map);                          // planned once from the starting map
        Assert.Equal(2, plan.Length);
        var live = new MapState(map);
        Assert.All(plan, p => Assert.True(PropPlanner.StillStands(p, live)));
        live.SetOverlay(new CellCoord(8, 8), OverlayKind.None, 0);                     // what DestroyProp does
        Assert.False(PropPlanner.StillStands(plan[0], live));
        Assert.True(PropPlanner.StillStands(plan[1], live));                           // the block is still there
    }
}
