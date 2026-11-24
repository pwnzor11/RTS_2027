// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-LE-08: lb_01 may not list lb_slinger as buildable; its tier-2 gate is never reached there. The start grant is untouched.</summary>
public sealed class LbSlingerBuildableTests
{
    [Fact]
    public void Only_these_may_be_built_list_leaves_out_the_slinger()
    {
        string root = RepoPath.Root;
        string toml = File.ReadAllText(Path.Combine(root, "data", "missions", "lb_01", "mission.toml"));
        int at = toml.IndexOf("Only these may be built", StringComparison.Ordinal);
        Assert.True(at >= 0, "the buildable list comment is gone");
        string buildable = toml[at..toml.IndexOf('\n', toml.IndexOf("tech = [", at, StringComparison.Ordinal))];
        Assert.DoesNotContain("lb_slinger", buildable);
        Assert.Contains("lb_hook_runner", buildable);
    }
}
