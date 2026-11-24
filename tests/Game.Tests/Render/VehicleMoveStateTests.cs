// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Render;
using Rts.Game.Sandbox;
using Rts.Game.Sprites;
using Rts.Game.Tests.Audio;
using Rts.Sim.Core;

namespace Rts.Game.Tests.Render;

/// <summary>Vehicle <c>move</c> state: chosen while moving, advanced by distance travelled, and falling back to idle for sheets without it.</summary>
public sealed class VehicleMoveStateTests
{
    private const string Header = "file = \"t.png\"\nframe_w = 32\nframe_h = 32\norigin_x = 16\norigin_y = 24\npx_per_cell = 80\n";
    private const string Idle = "[[state]]\nname = \"idle\"\nfacings = 32\nframes = 1\nfirst = 0\nticks_per_frame = 1\n";

    private static SpriteManifest WithMove(string extra = "step_px = 8") =>
        SpriteManifest.Parse(Header + Idle + $"[[state]]\nname = \"move\"\nfacings = 32\nframes = 4\nfirst = 32\nticks_per_frame = 1\n{extra}\n", "v", "v.sprite.toml");

    private static SpriteManifest IdleOnly() => SpriteManifest.Parse(Header + Idle, "v", "v.sprite.toml");

    [Fact]
    public void A_vehicle_without_a_move_state_draws_idle_even_while_moving()
    {
        SpriteManifest m = IdleOnly();

        Assert.Equal(m.FrameIndex("idle", 64, 7), SpriteStates.UnitFrame(m, false, true, 64, 7, 5000));
    }

    [Fact]
    public void A_vehicle_with_a_move_state_draws_idle_when_standing_and_move_when_moving()
    {
        SpriteManifest m = WithMove();

        Assert.Equal(m.FrameIndex("idle", 64, 0), SpriteStates.UnitFrame(m, false, false, 64, 0, 5000));
        int frame = SpriteStates.UnitFrame(m, false, true, 64, 0, 0);
        Assert.Equal(32 + 8 * 4, frame);   // facing 64 of 256 = facing 8 of 32, step 0
    }

    [Fact]
    public void Move_frames_advance_one_per_step_px_of_travel_and_wrap()
    {
        SpriteManifest m = WithMove("step_px = 8");   // 8 sheet px = 32 leptons (80 px sheet cell drawn 1.25x over a 256 lepton cell)
        int first = SpriteStates.UnitFrame(m, false, true, 0, 0, 0);

        Assert.Equal(first, SpriteStates.UnitFrame(m, false, true, 0, 99, 31));
        Assert.Equal(first + 1, SpriteStates.UnitFrame(m, false, true, 0, 0, 33));
        Assert.Equal(first + 3, SpriteStates.UnitFrame(m, false, true, 0, 0, 97));
        Assert.Equal(first, SpriteStates.UnitFrame(m, false, true, 0, 0, 129));   // 4 steps = one cycle
    }

    [Fact]
    public void A_move_state_without_step_px_advances_by_ticks()
    {
        SpriteManifest m = WithMove("");

        Assert.Equal(m.FrameIndex("move", 0, 2), SpriteStates.UnitFrame(m, false, true, 0, 2, 99999));
    }

    [Fact]
    public void Infantry_never_use_the_vehicle_move_state()
    {
        SpriteManifest m = WithMove();

        Assert.Equal(m.FrameIndex("idle", 0, 0), SpriteStates.UnitFrame(m, true, true, 0, 0, 500));
    }

    [Fact]
    public void The_layer_plan_passes_travel_to_the_body_frame()
    {
        SpriteManifest m = WithMove();

        UnitLayer body = Assert.Single(UnitLayerPlan.For(m, false, true, 0, null, false, 0, travelLeptons: 52));
        Assert.Equal(SpriteStates.UnitFrame(m, false, true, 0, 0, 52), body.Frame);
    }

    [Fact]
    public void Render_memory_accumulates_distance_only_while_a_unit_moves()
    {
        var memory = new RenderMemory();
        UnitView a = AudioTestSupport.Unit(1, 0, 3, 3);
        UnitKey key = UnitKey.Of(a);
        memory.Observe(AudioTestSupport.Snap(1, units: new[] { a }));
        Assert.Equal(0, memory.Travel(key));

        UnitView b = a with { Position = new LeptonCoord(a.Position.X + 30, a.Position.Y) };
        memory.Observe(AudioTestSupport.Snap(2, units: new[] { b }));
        Assert.Equal(30, memory.Travel(key));
        Assert.True(memory.IsMoving(key));

        memory.Observe(AudioTestSupport.Snap(3, units: new[] { b }));
        Assert.Equal(30, memory.Travel(key));
        Assert.False(memory.IsMoving(key));
    }

    [Fact]
    public void The_shipped_tank_and_jeep_sheets_have_a_move_state_with_a_step()
    {
        foreach (string id in new[] { "auth_tank", "auth_jeep" })
        {
            SpriteManifest m = SpriteManifest.Load(SpriteManifestTests.RepoFile("data", "sprites", "vehicles", id + ".sprite.toml"));
            SpriteState move = m.State("move");
            Assert.Equal(32, move.Facings);
            Assert.Equal(4, move.Frames);
            Assert.True(move.StepPx > 0);
        }
    }
}
