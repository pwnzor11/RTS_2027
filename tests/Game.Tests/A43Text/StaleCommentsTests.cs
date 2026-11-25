// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 TEXT-09: stale comments that contradict the shipped data and code.</summary>
public sealed class StaleCommentsTests
{
    private static string Root => RepoPath.Root;

    [Fact]
    public void Game_toml_does_not_say_the_Expedition_is_unplayable()
    {
        string game = File.ReadAllText(Path.Combine(Root, "data", "rules", "game.toml"));
        Assert.DoesNotContain("NOT playable yet", game);
    }

    [Fact]
    public void GameScreen_comments_name_the_real_bookmark_keys()
    {
        string screen = File.ReadAllText(Path.Combine(Root, "src", "Game", "Screens", "GameScreen.cs"));
        Assert.DoesNotContain("F5..F8", screen);
    }
}
