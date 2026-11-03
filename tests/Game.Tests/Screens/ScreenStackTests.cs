// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Game.Input;
using Rts.Game.Screens;

namespace Rts.Game.Tests.Screens;

public sealed class ScreenStackTests
{
    private sealed class Probe : IScreen
    {
        private readonly string _name;
        private readonly List<string> _log;

        public Probe(string name, List<string> log, bool blocksUpdate = false, bool blocksDraw = false)
        {
            _name = name;
            _log = log;
            BlocksUpdateBelow = blocksUpdate;
            BlocksDrawBelow = blocksDraw;
        }

        public bool BlocksUpdateBelow { get; }
        public bool BlocksDrawBelow { get; }
        public Action<ScreenStack>? OnUpdate { get; set; }
        public ScreenStack? Stack { get; set; }

        public void Enter() => _log.Add($"enter {_name}");
        public void Exit() => _log.Add($"exit {_name}");

        public void Update(InputFrame input, GameTime t)
        {
            _log.Add($"update {_name}");
            if (Stack is not null) OnUpdate?.Invoke(Stack);
        }

        public void Draw(SpriteBatch sb) => _log.Add($"draw {_name}");
    }

    private static InputFrame Frame() => new(new KeyboardState(), new KeyboardState(), new MouseState(), new MouseState());

    [Fact]
    public void Push_and_pop_call_enter_and_exit()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("a", log));
        stack.Push(new Probe("b", log));
        stack.Pop();
        Assert.Equal(new[] { "enter a", "enter b", "exit b" }, log);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void Update_runs_top_down_and_draw_runs_bottom_up()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("a", log));
        stack.Push(new Probe("b", log));
        stack.Push(new Probe("c", log));
        log.Clear();
        stack.Update(Frame(), new GameTime());
        stack.Draw(null!);
        Assert.Equal(new[] { "update c", "update b", "update a", "draw a", "draw b", "draw c" }, log);
    }

    [Fact]
    public void BlocksUpdateBelow_stops_the_update_pass()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("game", log));
        stack.Push(new Probe("pause", log, blocksUpdate: true));
        log.Clear();
        stack.Update(Frame(), new GameTime());
        Assert.Equal(new[] { "update pause" }, log);
    }

    [Fact]
    public void BlocksDrawBelow_hides_screens_underneath()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("menu", log, blocksDraw: true));
        stack.Push(new Probe("game", log, blocksDraw: true));
        stack.Push(new Probe("pause", log));
        log.Clear();
        stack.Draw(null!);
        Assert.Equal(new[] { "draw game", "draw pause" }, log);
    }

    [Fact]
    public void Changes_made_during_update_are_applied_after_the_pass()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        var bottom = new Probe("bottom", log);
        var top = new Probe("top", log) { Stack = stack };
        top.OnUpdate = s => s.Push(new Probe("new", log));
        stack.Push(bottom);
        stack.Push(top);
        log.Clear();
        stack.Update(Frame(), new GameTime());
        // The pushed screen is not updated in the pass that created it, and the iteration was not disturbed.
        Assert.Equal(new[] { "update top", "update bottom", "enter new" }, log);
        Assert.Equal(3, stack.Count);
    }

    [Fact]
    public void Replace_exits_the_old_top_then_enters_the_new_one()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("a", log));
        stack.Push(new Probe("b", log));
        log.Clear();
        stack.Replace(new Probe("c", log));
        Assert.Equal(new[] { "exit b", "enter c" }, log);
        Assert.Equal(2, stack.Count);
    }

    [Fact]
    public void ReplaceAll_exits_every_screen_top_down()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("a", log));
        stack.Push(new Probe("b", log));
        log.Clear();
        stack.ReplaceAll(new Probe("menu", log));
        Assert.Equal(new[] { "exit b", "exit a", "enter menu" }, log);
        Assert.Equal(1, stack.Count);
    }

    [Fact]
    public void Posted_actions_inside_update_run_in_order_afterwards()
    {
        var log = new List<string>();
        var stack = new ScreenStack();
        stack.Push(new Probe("a", log));
        var top = new Probe("b", log) { Stack = stack };
        top.OnUpdate = s => s.Post(() =>
        {
            s.Pop();
            s.Pop();
            s.Push(new Probe("fresh", log));
        });
        stack.Push(top);
        log.Clear();
        stack.Update(Frame(), new GameTime());
        Assert.Equal(new[] { "update b", "update a", "exit b", "exit a", "enter fresh" }, log);
    }
}
