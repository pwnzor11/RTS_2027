// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Tests.Archive;

namespace Rts.Game.Tests.A43Text;

/// <summary>A43 ROSTER-LE-07: no water building carries a footprint `water` mask, so the comments must not say they do.</summary>
public sealed class WaterBuildingCommentsTests
{
    [Fact]
    public void Buildings_toml_does_not_describe_a_water_mask_that_no_entry_has()
    {
        string root = RepoPath.Root;
        string text = File.ReadAllText(Path.Combine(root, "data", "rules", "buildings.toml"));
        Assert.DoesNotContain("`water` mask", text);
        Assert.DoesNotContain("row 0 land, row 1 water", text);
        Assert.DoesNotContain("(bottom row water)", text);
    }
}
