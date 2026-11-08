// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Tools;

namespace Rts.Tools.Tests;

/// <summary>M12-12: `ai match` / `ai timeline` / `ai matrix` take --layout 1|2 and --biome; no flag keeps the synthetic map.</summary>
public class AiMatchLayoutTests
{
    private static Dictionary<string, string> Flags(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    [Fact]
    public void No_flag_means_no_choice_so_the_synthetic_map_is_unchanged()
    {
        Assert.True(AiCommands.ParseMapChoice(Flags(), out var choice, out var error));
        Assert.Null(choice);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("2", 2)]
    public void Layout_is_read_and_the_biome_defaults_to_rust_belt(string text, int layout)
    {
        Assert.True(AiCommands.ParseMapChoice(Flags("layout", text), out var choice, out _));
        Assert.Equal(new AiCommands.MapChoice(Biome.RustBelt, layout), choice);
    }

    [Fact]
    public void Biome_alone_means_layout_1()
    {
        Assert.True(AiCommands.ParseMapChoice(Flags("biome", "ash_plains"), out var choice, out _));
        Assert.Equal(new AiCommands.MapChoice(Biome.AshPlains, MapGenerator.CurrentLayout), choice);
    }

    [Theory]
    [InlineData("layout", "3")]
    [InlineData("layout", "x")]
    [InlineData("biome", "moon")]
    public void Bad_values_are_refused(string key, string value)
    {
        Assert.False(AiCommands.ParseMapChoice(Flags(key, value), out var choice, out var error));
        Assert.Null(choice);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Layout_2_builds_the_zoned_map_and_layout_1_the_classic_one()
    {
        var one = new AiCommands.MapChoice(Biome.RustBelt, 1).Params(96, 2, 3);
        var two = new AiCommands.MapChoice(Biome.RustBelt, 2).Params(96, 2, 3);
        Assert.Equal(1, one.Layout);
        Assert.Equal(2, two.Layout);
        Assert.Equal(MapSize.Medium, two.Size);
        Assert.NotEqual(MapGenerator.Generate(one).Land.ToArray(), MapGenerator.Generate(two).Land.ToArray());
    }

    [Fact]
    public void A_short_match_on_layout_2_runs_and_the_table_names_the_layout()
    {
        string file = Path.Combine(Path.GetTempPath(), "rts_layout_match_" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            int code = AiCommands.Run(["match", "--a", "tiered:3", "--b", "tiered:3", "--seeds", "1", "--ticks", "300", "--size", "96",
                "--layout", "2", "--biome", "mountains", "--out", file]);
            Assert.Equal(0, code);
            Assert.Contains("mountains layout 2", File.ReadAllText(file));
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
