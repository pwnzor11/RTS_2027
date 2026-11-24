// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 TEXT-04: there is no Barracks tab; infantry is built on the Infantry tab (key E).</summary>
public sealed class TutorialInfantryTabTests
{
    private static string Root => RepoPath.Root;

    [Fact]
    public void Train_step_and_hint_point_at_the_Infantry_tab()
    {
        string locale = File.ReadAllText(Path.Combine(Root, "data", "missions", "tut_01", "locale.toml"));
        Assert.DoesNotContain("Barracks tab", locale);
        Assert.DoesNotContain("select the Barracks", locale);
        Assert.Contains("Infantry tab", locale);
    }
}
