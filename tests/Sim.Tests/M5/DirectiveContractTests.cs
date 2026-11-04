// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Tests.Determinism;
using Xunit;

namespace Rts.Sim.Tests.M5;

/// <summary>M5-01 directive contract (docs/design/09_m5_plan.md §2–§3): command, clamping, the "directive" part and the player view.</summary>
public class DirectiveContractTests
{
    private static readonly CellCoord Explored = new(3, 3);    // next to player 0's starting vehicles
    private static readonly CellCoord Shrouded = new(62, 20);  // far from everything player 0 owns

    private static Simulation Warm()
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int i = 0; i < 3; i++) sim.Step(Array.Empty<Command>());
        return sim;
    }

    private static void Send(Simulation sim, byte player, int arg, CellCoord cell) =>
        sim.Step(new[] { new Command(sim.Tick, player, 1, CommandKind.Directive, default, default, cell, arg) });

    [Fact]
    public void CommandKindIsPinned() => Assert.Equal(23, (int)CommandKind.Directive);

    [Theory]
    [InlineData(0, 0, 0, 0, false)]
    [InlineData(3, 4, 1, 4, true)]
    [InlineData(2, 2, 255, 3, false)]
    [InlineData(15, 15, 200, 15, true)]
    public void PackUnpack_RoundTrips(byte posture, byte focus, byte target, byte mix, bool expand)
    {
        int arg = DirectiveCodec.Pack(posture, focus, target, mix, expand);
        Assert.Equal((posture, focus, target, mix, expand), DirectiveCodec.Unpack(arg));
    }

    [Fact]
    public void BeforeAnyDirective_ViewShowsNone()
    {
        var sim = Warm();
        Directive d = sim.ViewFor(0).Directive;
        Assert.False(d.Set);
        Assert.Equal(Directive.None, d);
        Assert.False(d.HasFocusCell);
    }

    [Fact]
    public void Directive_IsStoredForItsPlayerOnly_WithTickAndExploredFocusCell()
    {
        var sim = Warm();
        Assert.Equal(0, sim.Vision.VisionAt(0, Shrouded));   // fixture guard: the far cell really is unexplored
        Assert.NotEqual(0, sim.Vision.VisionAt(0, Explored));
        int tick = sim.Tick;
        Send(sim, 0, DirectiveCodec.Pack(DirectiveCodec.PostureAllIn, DirectiveCodec.FocusTech, 1, DirectiveCodec.MixVehicles, true), Explored);
        Directive d = sim.ViewFor(0).Directive;
        Assert.Equal(new Directive(true, 3, 1, 1, 2, true, Explored, tick), d);
        Assert.Equal(Directive.None, sim.ViewFor(1).Directive);
    }

    [Fact]
    public void LaterDirective_ReplacesTheEarlierOne()
    {
        var sim = Warm();
        Send(sim, 0, DirectiveCodec.Pack(3, 1, 1, 2, true), Explored);
        Send(sim, 0, DirectiveCodec.Pack(0, 0, 255, 0, false), Directive.NoCell);
        Directive d = sim.ViewFor(0).Directive;
        Assert.Equal((DirectiveCodec.PostureTurtle, DirectiveCodec.FocusEconomy, DirectiveCodec.AnyTarget, false), (d.Posture, d.Focus, d.TargetPlayer, d.Expand));
        Assert.False(d.HasFocusCell);
    }

    [Theory]
    [InlineData(9, 7, 1, 9, 1, 2, 1, 0)]     // posture, focus, mix out of range → balanced, army, auto
    [InlineData(2, 3, 0, 1, 2, 3, 255, 1)]   // target = self → any
    [InlineData(2, 3, 7, 1, 2, 3, 255, 1)]   // target past the last slot → any
    public void OutOfRangeValues_Clamp(byte posture, byte focus, byte target, byte mix, byte wantPosture, byte wantFocus, byte wantTarget, byte wantMix)
    {
        var sim = Warm();
        Send(sim, 0, DirectiveCodec.Pack(posture, focus, target, mix, false), Directive.NoCell);
        Directive d = sim.ViewFor(0).Directive;
        Assert.Equal((wantPosture, wantFocus, wantTarget, wantMix), (d.Posture, d.Focus, d.TargetPlayer, d.Mix));
    }

    [Fact]
    public void FocusCell_InShroudOrOffMap_IsDropped()
    {
        var sim = Warm();
        Send(sim, 0, DirectiveCodec.Pack(2, 2, 1, 0, false), Shrouded);
        Assert.False(sim.ViewFor(0).Directive.HasFocusCell);
        Assert.True(sim.ViewFor(0).Directive.Set);
        Send(sim, 0, DirectiveCodec.Pack(2, 2, 1, 0, false), new CellCoord(500, 3));
        Assert.False(sim.ViewFor(0).Directive.HasFocusCell);
    }

    [Fact]
    public void Directive_ChangesOnlyTheDirectivePart()
    {
        var a = Warm();
        var b = Warm();
        Send(a, 0, DirectiveCodec.Pack(3, 4, 1, 4, true), Explored);
        b.Step(Array.Empty<Command>());
        for (int i = 0; i < 40; i++) { a.Step(Array.Empty<Command>()); b.Step(Array.Empty<Command>()); }
        Assert.Equal("directive", Simulation.FirstDifferentPart(a.ComputeSubHashes(), b.ComputeSubHashes()));
        var diff = a.ComputeSubHashes().Zip(b.ComputeSubHashes()).Where(p => p.First.Hash != p.Second.Hash).Select(p => p.First.Part).ToArray();
        Assert.Equal(new[] { "directive" }, diff);
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void Directive_SurvivesSaveAndLoad()
    {
        var sim = Warm();
        Send(sim, 1, DirectiveCodec.Pack(1, 3, 0, 3, false), Directive.NoCell);
        var ms = new MemoryStream();
        sim.Save(ms);
        ms.Position = 0;
        var loaded = Simulation.Load(ms, sim.Setup);
        Assert.Equal(sim.ViewFor(1).Directive, loaded.ViewFor(1).Directive);
        Assert.Equal(sim.ComputeStateHash(), loaded.ComputeStateHash());
    }
}
