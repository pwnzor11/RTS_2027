using Rts.Data;
using Rts.Sim.Core;
using Xunit;

namespace Rts.Sim.Tests.Rules;

/// <summary>Tests for the TOML rules loader, its validation and the rules hash.</summary>
public sealed class RulesLoaderTests
{
    private const string ValidGame =
        "[game]\ntick_rate = 20\ndefault_map_width = 32\ndefault_map_height = 32\n";

    private const string ValidUnits =
        "[test_crawler]\nspeed = 16\nmax_health = 300\n";

    private static string RealRulesDir()
    {
        string? dir = RulesLocator.FindRulesDir(AppContext.BaseDirectory);
        Assert.NotNull(dir);
        return dir!;
    }

    [Fact]
    public void LoadsRealRulesFiles()
    {
        RuleSet rules = RulesLoader.Load(RealRulesDir());

        Assert.Equal(20, rules.Game.TickRate);
        Assert.Equal(32, rules.Game.DefaultMapWidth);
        Assert.Equal(32, rules.Game.DefaultMapHeight);
        var unit = Assert.Single(rules.Units);
        Assert.Equal(new UnitTypeDef("test_crawler", 16, 300), unit);
        Assert.NotEqual(0UL, rules.RulesHash);
    }

    [Fact]
    public void HashIsStableUnderReformatting()
    {
        using var temp = new TempRules(
            game: "# reformatted copy\n[game]\ndefault_map_height = 32   # rows\ndefault_map_width=32\n\n\ntick_rate =   20\n",
            units: "# comment\n\n[test_crawler]\nmax_health=300\n  speed =   16 # leptons\n");

        ulong original = RulesLoader.Load(RealRulesDir()).RulesHash;
        ulong reformatted = RulesLoader.Load(temp.Dir).RulesHash;

        Assert.Equal(original, reformatted);
    }

    [Fact]
    public void HashChangesWhenSpeedChanges()
    {
        using var baseline = new TempRules(ValidGame, ValidUnits);
        using var changed = new TempRules(ValidGame, "[test_crawler]\nspeed = 17\nmax_health = 300\n");

        Assert.NotEqual(RulesLoader.Load(baseline.Dir).RulesHash, RulesLoader.Load(changed.Dir).RulesHash);
    }

    [Theory]
    [InlineData("units.toml", "[test_crawler]\nspeed = 16\nmax_health = 300\narmour = 2\n", "test_crawler.armour", "unknown key")]
    [InlineData("units.toml", "[test_crawler]\nspeed = 16\n", "test_crawler.max_health", "missing required key")]
    [InlineData("units.toml", "[test_crawler]\nspeed = \"16\"\nmax_health = 300\n", "test_crawler.speed", "expected an integer")]
    [InlineData("units.toml", "[test_crawler]\nspeed = 0\nmax_health = 300\n", "test_crawler.speed", "must be between")]
    [InlineData("units.toml", "[test_crawler]\nspeed = 16\nmax_health = -1\n", "test_crawler.max_health", "must be between")]
    [InlineData("game.toml", "[game]\ntick_rate = 30\ndefault_map_width = 32\ndefault_map_height = 32\n", "game.tick_rate", "must be 20")]
    [InlineData("game.toml", "[game]\ntick_rate = 20\ndefault_map_width = 32\ndefault_map_height = 32\nextra = 1\n", "game.extra", "unknown key")]
    [InlineData("game.toml", "[game]\ntick_rate = 20\ndefault_map_height = 32\n", "game.default_map_width", "missing required key")]
    public void ValidationErrorsReportFileAndKey(string file, string content, string expectedKey, string expectedMessagePart)
    {
        string game = file == "game.toml" ? content : ValidGame;
        string units = file == "units.toml" ? content : ValidUnits;
        using var temp = new TempRules(game, units);

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        RulesProblem problem = Assert.Single(ex.Problems, p => p.Key == expectedKey);
        Assert.Equal(file, problem.File);
        Assert.Contains(expectedMessagePart, problem.Message);
    }

    [Fact]
    public void ReportsAllProblemsNotJustTheFirst()
    {
        using var temp = new TempRules(
            game: "[game]\ntick_rate = 30\ndefault_map_width = 32\n",
            units: "[test_crawler]\nspeed = 0\nmax_health = \"x\"\n");

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        Assert.Contains(ex.Problems, p => p.File == "game.toml" && p.Key == "game.tick_rate");
        Assert.Contains(ex.Problems, p => p.File == "game.toml" && p.Key == "game.default_map_height");
        Assert.Contains(ex.Problems, p => p.File == "units.toml" && p.Key == "test_crawler.speed");
        Assert.Contains(ex.Problems, p => p.File == "units.toml" && p.Key == "test_crawler.max_health");
    }

    [Fact]
    public void MissingFileIsReported()
    {
        using var temp = new TempRules(ValidGame, null);

        var ex = Assert.Throws<RulesException>(() => RulesLoader.Load(temp.Dir));

        Assert.Contains(ex.Problems, p => p.File == "units.toml" && p.Key == "<file>");
    }

    /// <summary>A rules directory written under the test output folder, deleted on dispose.</summary>
    private sealed class TempRules : IDisposable
    {
        public TempRules(string game, string? units)
        {
            Dir = Path.Combine(AppContext.BaseDirectory, "rules-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Path.Combine(Dir, RulesLoader.GameFile), game);
            if (units is not null)
            {
                File.WriteAllText(Path.Combine(Dir, RulesLoader.UnitsFile), units);
            }
        }

        public string Dir { get; }

        public void Dispose()
        {
            if (Directory.Exists(Dir))
            {
                Directory.Delete(Dir, recursive: true);
            }
        }
    }
}
