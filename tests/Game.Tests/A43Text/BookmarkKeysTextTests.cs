// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Menu;
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 TEXT-03 / GAMEUI-04: the camera bookmark keys named to players are F6, F7, F8 and F12; F5 is Quicksave.</summary>
public sealed class BookmarkKeysTextTests
{
    private static string Root => RepoPath.Root;

    [Fact]
    public void Loading_tip_names_the_real_bookmark_keys()
    {
        string tips = File.ReadAllText(Path.Combine(Root, "data", "ui", "tips.toml"));
        Assert.DoesNotContain("F5 to F8", tips);
        Assert.Contains("F6, F7, F8 or F12", tips);
    }

    [Fact]
    public void How_to_play_hotkeys_name_the_real_bookmark_keys()
    {
        string guide = string.Join("\n", HowToPlayContent.Pages.SelectMany(p => p.Lines));
        Assert.DoesNotContain("Ctrl+F5", guide);
        Assert.Contains("F6, F7, F8 or F12", guide);
    }
}
