// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Data;
using Rts.Game.Audio;
using Rts.Game.Input;
using Rts.Game.Menu;
using Rts.Game.Saves;
using Rts.Game.Sandbox;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Game.Ui;

namespace Rts.Game.Tests.S18;

/// <summary>S18 GS-10: a damaged map, mission or save becomes a Notice, never a crash, and Restart never leaves the stack torn down.</summary>
[Collection("UiStatics")]
public sealed class MenuGuardTests
{
    private sealed class FakeScreen : IScreen
    {
        public bool BlocksUpdateBelow => true;
        public bool BlocksDrawBelow => true;
        public bool Exited { get; private set; }
        public void Enter() { }
        public void Exit() => Exited = true;
        public void Update(InputFrame input, GameTime t) { }
        public void Draw(SpriteBatch sb) { }
    }

    // GameServices without a window: the stack, the settings and the rules folder are all these tests touch.
    private static GameServices Services(ScreenStack stack, string rulesDir) =>
        new(null!, null!, null!, rulesDir, NullAudio.Instance, new Settings(), stack, () => { });

    private static T WithStatics<T>(Func<T> body)
    {
        float before = UiScale.TextFactor;
        try { return body(); }
        finally { UiScale.TextFactor = before; }
    }

    [Fact]
    public void Data_errors_are_the_ones_loaders_throw_and_programming_errors_are_not()
    {
        foreach (Exception e in new Exception[]
        {
            new IOException("x"), new UnauthorizedAccessException("x"), new InvalidDataException("x"), new InvalidOperationException("x"),
            new FormatException("x"), new KeyNotFoundException("x"), new MapFileException("f", "x"), new SaveLoadException("x"),
            new RulesException("f", "k", "x"),
        })
            Assert.True(ScreenGuard.IsDataError(e), e.GetType().Name);
        Assert.False(ScreenGuard.IsDataError(new NullReferenceException()));
        Assert.False(ScreenGuard.IsDataError(new OutOfMemoryException()));
    }

    [Fact]
    public void A_factory_that_hits_a_damaged_map_gives_a_notice_and_no_screen() => WithStatics(() =>
    {
        var stack = new ScreenStack();
        GameServices s = Services(stack, "data/rules");
        IScreen? made = ScreenGuard.TryCreate(s, "Cannot open", () => throw new MapFileException("rustbelt_m2.map.toml", "not a map"));
        Assert.Null(made);
        Assert.IsType<NoticeScreen>(stack.Top);
        return 0;
    });

    [Fact]
    public void A_factory_that_works_is_returned_and_nothing_is_pushed() => WithStatics(() =>
    {
        var stack = new ScreenStack();
        var screen = new FakeScreen();
        Assert.Same(screen, ScreenGuard.TryCreate(Services(stack, "data/rules"), "t", () => screen));
        Assert.Equal(0, stack.Count);
        return 0;
    });

    [Fact]
    public void A_programming_error_in_a_factory_is_not_swallowed()
    {
        var stack = new ScreenStack();
        Assert.Throws<NullReferenceException>(() => WithStatics(() => ScreenGuard.TryCreate(Services(stack, "data/rules"), "t", () => throw new NullReferenceException())));
    }

    [Fact]
    public void Restart_with_a_failing_factory_keeps_the_menu_below_and_shows_why() => WithStatics(() =>
    {
        var stack = new ScreenStack();
        var menu = new FakeScreen();
        var game = new FakeScreen();
        stack.Push(menu);
        stack.Push(game);
        stack.Push(new FakeScreen());   // the pause menu
        GameFlow.Restart(Services(stack, "data/rules"), () => throw new MapFileException("m.map.toml", "damaged"), () => new FakeScreen());
        Assert.True(game.Exited);
        Assert.IsType<NoticeScreen>(stack.Top);
        Assert.Same(menu, stack.Screens[0]);
        Assert.Equal(2, stack.Count);
        return 0;
    });

    [Fact]
    public void Restart_with_a_failing_factory_never_leaves_an_empty_stack() => WithStatics(() =>
    {
        var stack = new ScreenStack();
        stack.Push(new FakeScreen());   // the game
        stack.Push(new FakeScreen());   // the pause menu
        var fallback = new FakeScreen();
        GameFlow.Restart(Services(stack, "data/rules"), () => throw new IOException("disk"), () => fallback);
        Assert.Same(fallback, stack.Screens[0]);
        Assert.IsType<NoticeScreen>(stack.Top);
        return 0;
    });

    [Fact]
    public void Restart_that_works_replaces_the_game() => WithStatics(() =>
    {
        var stack = new ScreenStack();
        var fresh = new FakeScreen();
        stack.Push(new FakeScreen());
        stack.Push(new FakeScreen());
        GameFlow.Restart(Services(stack, "data/rules"), () => fresh);
        Assert.Same(fresh, stack.Top);
        Assert.Equal(1, stack.Count);
        return 0;
    });

    // The mission save is real; only the mission's map file in a private copy of the data folder is damaged.
    [Fact]
    public void A_mission_save_whose_map_file_is_damaged_is_refused_with_a_SaveLoadException()
    {
        using var t = new SaveTestSupport();
        MissionGame game = MissionGame.Create(t.RulesDir, "auth_01");
        var host = new Rts.Game.Host.SimHost(game.Sim);
        host.StepTicks(5);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(), host, t.RulesDir, "m");

        string source = Path.GetFullPath(Path.Combine(t.RulesDir, ".."));
        string copy = Path.Combine(t.Folder, "data");
        foreach (string dir in new[] { "rules", "ai", "missions", "maps" })
            CopyDir(Path.Combine(source, dir), Path.Combine(copy, dir));
        File.WriteAllText(Path.Combine(copy, "maps", "rustbelt_m2.map.toml"), "not a map [[[");

        var e = Assert.Throws<SaveLoadException>(() => GameLoader.Load(path, Path.Combine(copy, "rules")));
        Assert.Contains("rustbelt_m2", e.Message);
    }

    private static void CopyDir(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)), overwrite: true);
        foreach (string dir in Directory.GetDirectories(from)) CopyDir(dir, Path.Combine(to, Path.GetFileName(dir)));
    }
}
