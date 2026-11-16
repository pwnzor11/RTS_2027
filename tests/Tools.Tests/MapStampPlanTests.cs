// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>M12-14: each reworked campaign map is exactly its pre-rework base (tools/maps/base) plus its stamp plan (tools/maps/plans), so the
/// map file can be reviewed, and rebuilt, as a stamp list. A hand edit of a data map, or a plan change without a rebuild, fails here.</summary>
[Collection("ToolsConsole")]
public class MapStampPlanTests
{
    private static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "RTS_2027.slnx"))) return d.FullName;
        throw new InvalidOperationException("repo root not found");
    }

    [Theory]
    [InlineData("rustbelt_m2")]
    [InlineData("glass_coast_e1")]
    [InlineData("ash_reckoning")]
    public void Data_map_equals_base_plus_plan(string id)
    {
        string root = Root();
        string outPath = Path.Combine(Path.GetTempPath(), "stamp_" + Guid.NewGuid().ToString("N") + ".map.toml");
        var old = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            Assert.Equal(0, MapStampCommand.Run(["--base", Path.Combine(root, "tools", "maps", "base", id + ".map.toml"),
                "--plan", Path.Combine(root, "tools", "maps", "plans", id + ".stamps.toml"), "--out", outPath]));
        }
        finally { Console.SetOut(old); }
        try { Assert.Equal(File.ReadAllText(Path.Combine(root, "data", "maps", id + ".map.toml")), File.ReadAllText(outPath)); }
        finally { File.Delete(outPath); }
    }
}
