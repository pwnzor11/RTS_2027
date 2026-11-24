// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 TEXT-07: attack-move is armed with A and the next click is a right-click on the ground.</summary>
public sealed class AttackMoveTextTests
{
    private static string Root => RepoPath.Root;

    [Fact]
    public void Controls_screen_line_says_right_click()
    {
        string text = GameKeys.InfoOf(GameAction.AttackMove).Description;
        Assert.Contains("right-click", text);
        Assert.DoesNotContain("then click", text);
    }

    [Fact]
    public void README_controls_say_right_click_for_attack_move()
    {
        string readme = File.ReadAllText(Path.Combine(Root, "README.md"));
        Assert.DoesNotContain("**A** then click", readme);
        Assert.Contains("**A** then right-click", readme);
    }
}
