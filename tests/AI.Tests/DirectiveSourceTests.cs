// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.AI;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.View;

namespace AI.Tests;

/// <summary>M5 contract: <see cref="AiDriver"/> turns an <see cref="IDirectiveSource"/> result into a recorded Directive command.</summary>
public class DirectiveSourceTests
{
    private sealed class Idle : IAiPlayer
    {
        public string Id => "idle";
        public void Start(AiContext ctx) { }
        public void Think(PlayerView view, ICommandSink sink) => sink.Issue(CommandKind.Stop, new EntityId(1, 1), ActorKind.Vehicle);
    }

    private sealed class Scripted(int onTick, int arg) : IDirectiveSource
    {
        public int Polls;
        public bool Poll(PlayerView view, out int packedArg, out CellCoord focusCell)
        {
            Polls++;
            packedArg = arg; focusCell = Directive.NoCell;
            return view.Tick == onTick;
        }
    }

    [Fact]
    public void SourceIsPolledEveryTick_AndItsDirectiveGetsTheSlotsDelayAndSequence()
    {
        var sim = TestWorld.FlatSim();
        var driver = new AiDriver(5);
        driver.Add(0, new Idle(), DifficultyProfile.Default with { DecisionIntervalTicks = 4, CommandDelayTicks = 2 });
        int arg = DirectiveCodec.Pack(DirectiveCodec.PostureAllIn, DirectiveCodec.FocusArmy, 1, DirectiveCodec.MixAuto, false);
        var src = new Scripted(onTick: 2, arg);
        driver.SetDirectiveSource(0, src);
        var all = new List<Command>();
        for (int t = 0; t < 6; t++)
        {
            var cmds = new List<Command>();
            driver.Collect(sim, cmds);
            all.AddRange(cmds);
            sim.Step(cmds.Where(c => c.ExecTick == sim.Tick).ToArray());   // delay 2: these run later; the sim ignores stale ExecTicks anyway
        }
        Assert.Equal(6, src.Polls);
        Command d = Assert.Single(all, c => c.Kind == CommandKind.Directive);
        Assert.Equal(4, d.ExecTick);              // tick 2 + delay 2
        Assert.Equal(1, d.Sequence);              // after the tick-0 Stop
        Assert.Equal(arg, d.Arg);
        Assert.Equal(Directive.NoCell, d.Cell);
    }

    [Fact]
    public void SettingASourceOnAnEmptySlotThrows()
    {
        var driver = new AiDriver(1);
        Assert.Throws<ArgumentException>(() => driver.SetDirectiveSource(3, new Scripted(0, 0)));
    }

    [Fact]
    public void DirectiveReachesTheView()
    {
        var sim = TestWorld.FlatSim();
        var driver = new AiDriver(5);
        driver.Add(0, new Idle(), DifficultyProfile.Default with { DecisionIntervalTicks = 100, CommandDelayTicks = 1 });
        int arg = DirectiveCodec.Pack(DirectiveCodec.PostureTurtle, DirectiveCodec.FocusTech, 255, DirectiveCodec.MixAntiAir, true);
        driver.SetDirectiveSource(0, new Scripted(onTick: 0, arg));
        var pending = new List<Command>();
        for (int t = 0; t < 3; t++)
        {
            var cmds = new List<Command>();
            driver.Collect(sim, cmds);
            pending.AddRange(cmds);
            var due = pending.Where(c => c.ExecTick == sim.Tick).ToArray();
            pending.RemoveAll(c => c.ExecTick == sim.Tick);
            sim.Step(due);
        }
        Directive v = sim.ViewFor(0).Directive;
        Assert.True(v.Set);
        Assert.Equal((DirectiveCodec.PostureTurtle, DirectiveCodec.FocusTech, DirectiveCodec.MixAntiAir, true), (v.Posture, v.Focus, v.Mix, v.Expand));
    }
}
