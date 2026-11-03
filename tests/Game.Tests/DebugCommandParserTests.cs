using Rts.Game.Ui;
using Rts.Sim.Core;

namespace Rts.Game.Tests;

/// <summary>Checks the debug console's line parser: valid commands, defaults, bad numbers, unknown words and ids.</summary>
public sealed class DebugCommandParserTests
{
    private static readonly DebugCatalog Catalog = new(
        new[] { "test_tank", "test_jeep" },
        new[] { "test_rifleman" },
        PlayerCount: 2);

    private static DebugCommand Accepted(string line)
    {
        DebugParseResult result = DebugCommandParser.Parse(line, Catalog);
        Assert.True(result.Succeeded, $"expected '{line}' to parse, got: {result.Error}");
        return result.Command!;
    }

    private static string Rejected(string line)
    {
        DebugParseResult result = DebugCommandParser.Parse(line, Catalog);
        Assert.False(result.Succeeded, $"expected '{line}' to be rejected");
        return result.Error!;
    }

    [Fact]
    public void Spawn_WithCount_ResolvesTypeIndexAndDefaultsPlayer()
    {
        var spawn = Assert.IsType<SpawnCommand>(Accepted("spawn vehicle test_tank 3"));
        Assert.Equal(ActorKind.Vehicle, spawn.Kind);
        Assert.Equal(0, spawn.TypeIndex);
        Assert.Equal(3, spawn.Count);
        Assert.Equal(0, spawn.Player);
    }

    [Fact]
    public void Spawn_CountAndPlayerDefault_ToOneAndZero()
    {
        var spawn = Assert.IsType<SpawnCommand>(Accepted("spawn vehicle test_jeep"));
        Assert.Equal(1, spawn.Count);
        Assert.Equal(0, spawn.Player);
        Assert.Equal(1, spawn.TypeIndex);
    }

    [Fact]
    public void Spawn_WithPlayer_ParsesPlayer()
    {
        var spawn = Assert.IsType<SpawnCommand>(Accepted("spawn infantry test_rifleman 5 1"));
        Assert.Equal(ActorKind.Infantry, spawn.Kind);
        Assert.Equal(0, spawn.TypeIndex);
        Assert.Equal(5, spawn.Count);
        Assert.Equal(1, spawn.Player);
    }

    [Fact]
    public void Spawn_IsCaseInsensitiveAndTolerantOfExtraSpaces()
    {
        var spawn = Assert.IsType<SpawnCommand>(Accepted("  SPAWN   Vehicle   TEST_TANK   2  "));
        Assert.Equal(2, spawn.Count);
        Assert.Equal("test_tank", spawn.TypeId);
    }

    [Theory]
    [InlineData("spawn vehicle test_tank 0")]
    [InlineData("spawn vehicle test_tank 51")]
    [InlineData("spawn vehicle test_tank x")]
    [InlineData("spawn vehicle test_tank 1.5")]
    [InlineData("spawn vehicle test_tank 2 2")]
    [InlineData("spawn vehicle test_tank 2 -1")]
    public void Spawn_BadCountOrPlayer_IsRejected(string line)
    {
        Assert.NotEmpty(Rejected(line));
    }

    [Fact]
    public void Spawn_UnknownTypeId_ListsValidIds()
    {
        string error = Rejected("spawn vehicle test_bogus");
        Assert.Contains("test_bogus", error);
        Assert.Contains("test_tank", error);
        Assert.Contains("test_jeep", error);
    }

    [Fact]
    public void Spawn_UnknownKind_IsRejected()
    {
        Assert.Contains("vehicle or infantry", Rejected("spawn building test_tank"));
    }

    [Fact]
    public void Spawn_MissingArguments_ShowsUsage()
    {
        Assert.Contains("usage: spawn", Rejected("spawn vehicle"));
    }

    [Fact]
    public void Cash_ParsesAmountAndDefaultPlayer()
    {
        var cash = Assert.IsType<CashCommand>(Accepted("cash 5000"));
        Assert.Equal(5000, cash.Amount);
        Assert.Equal(0, cash.Player);
        Assert.Equal(1, Assert.IsType<CashCommand>(Accepted("cash 0 1")).Player);
    }

    [Theory]
    [InlineData("cash -5")]
    [InlineData("cash lots")]
    [InlineData("cash 99999999999")]
    [InlineData("cash 10 7")]
    public void Cash_BadAmountOrPlayer_IsRejected(string line)
    {
        Assert.NotEmpty(Rejected(line));
    }

    [Fact]
    public void Kill_Reveal_Pause_Help_TakeNoArguments()
    {
        Assert.IsType<KillCommand>(Accepted("kill"));
        Assert.IsType<RevealCommand>(Accepted("reveal"));
        Assert.IsType<PauseCommand>(Accepted("pause"));
        Assert.IsType<HelpCommand>(Accepted("help"));
        Assert.NotEmpty(Rejected("kill now"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Speed_AcceptsTheFourSteps(int multiplier)
    {
        Assert.Equal(multiplier, Assert.IsType<SpeedCommand>(Accepted($"speed {multiplier}")).Multiplier);
    }

    [Theory]
    [InlineData("speed 3")]
    [InlineData("speed 0")]
    [InlineData("speed fast")]
    [InlineData("speed")]
    public void Speed_OtherValues_AreRejected(string line)
    {
        Assert.NotEmpty(Rejected(line));
    }

    [Fact]
    public void Step_DefaultsToOneTick_AndTakesACount()
    {
        Assert.Equal(1, Assert.IsType<StepCommand>(Accepted("step")).Ticks);
        Assert.Equal(25, Assert.IsType<StepCommand>(Accepted("step 25")).Ticks);
    }

    [Theory]
    [InlineData("step 0")]
    [InlineData("step 1001")]
    [InlineData("step many")]
    [InlineData("step 1 2")]
    public void Step_BadCount_IsRejected(string line)
    {
        Assert.NotEmpty(Rejected(line));
    }

    [Fact]
    public void UnknownCommand_IsRejectedWithHint()
    {
        Assert.Contains("unknown command 'fly'", Rejected("fly"));
    }

    [Fact]
    public void EmptyLine_IsRejected()
    {
        Assert.NotEmpty(Rejected("   "));
    }

    [Fact]
    public void HelpLines_ListEveryTypeId()
    {
        string text = string.Join("\n", DebugCommandParser.HelpLines(Catalog));
        Assert.Contains("test_tank", text);
        Assert.Contains("test_jeep", text);
        Assert.Contains("test_rifleman", text);
    }
}
