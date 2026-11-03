using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Golden;

/// <summary>
/// Golden replay: a fixed scripted skirmish on the test_32 fixture map. Records the state hash every 16 ticks and
/// compares the list with <c>golden_hashes.txt</c>, so every change in sim behaviour is an explicit file change.
/// </summary>
public class GoldenTests
{
    private const string UpdateVariable = "RTS_UPDATE_GOLDEN";
    private const int TotalTicks = 1200;
    private const int HashInterval = 16;
    private const int AttackMoveTick = 40;
    private const string FailureHint =
        "Sim behaviour changed. If intended, rerun with RTS_UPDATE_GOLDEN=1 and commit golden_hashes.txt with a note in the commit message.";

    [Fact]
    public void ScriptedSkirmish_MatchesGoldenHashes()
    {
        List<string> actual = RunScenario();
        string path = GoldenPath();

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, string.Join("\n", actual) + "\n");
            return;
        }

        Assert.True(File.Exists(path), $"{FailureHint} Golden file not found: {path}");
        string[] expected = File.ReadAllLines(path);
        int common = Math.Min(expected.Length, actual.Count);
        for (int i = 0; i < common; i++)
        {
            if (expected[i] != actual[i])
            {
                Assert.Fail($"{FailureHint}\nFirst difference at line {i + 1}: expected '{expected[i]}', actual '{actual[i]}'.");
            }
        }
        Assert.True(expected.Length == actual.Count,
            $"{FailureHint}\nLine count differs: expected {expected.Length}, actual {actual.Count}.");
    }

    /// <summary>Runs the fixed scenario and returns one <c>tick hash_hex</c> line per recorded tick.</summary>
    private static List<string> RunScenario()
    {
        RuleSet rules = RulesLoader.Load(TestData.RulesDir);
        MapData map = MapFile.Load(Path.Combine(TestData.MapsDir, "test_32.map.toml"));
        var sim = new Simulation(new SimSetup(1, map, 2, rules.Sim, rules.RulesHash));

        int tank = SimRules.IndexOf(rules.Sim.Vehicles, "test_tank", v => v.Id);
        int jeep = SimRules.IndexOf(rules.Sim.Vehicles, "test_jeep", v => v.Id);
        int rifle = SimRules.IndexOf(rules.Sim.Infantry, "test_rifleman", i => i.Id);

        // Player 0 starts near (4,4), player 1 near (27,27). Every spawn and destination cell is clear ground.
        var player0 = new List<(EntityId Id, ActorKind Kind)>
        {
            (sim.SpawnVehicle(0, tank, new CellCoord(3, 5)), ActorKind.Vehicle),
            (sim.SpawnVehicle(0, tank, new CellCoord(5, 5)), ActorKind.Vehicle),
            (sim.SpawnVehicle(0, jeep, new CellCoord(4, 7)), ActorKind.Vehicle),
            (sim.SpawnInfantry(0, rifle, new CellCoord(2, 3), 0), ActorKind.Infantry),
            (sim.SpawnInfantry(0, rifle, new CellCoord(6, 3), 0), ActorKind.Infantry),
        };
        var player1 = new List<(EntityId Id, ActorKind Kind)>
        {
            (sim.SpawnVehicle(1, tank, new CellCoord(26, 26)), ActorKind.Vehicle),
            (sim.SpawnVehicle(1, tank, new CellCoord(28, 26)), ActorKind.Vehicle),
            (sim.SpawnVehicle(1, jeep, new CellCoord(27, 29)), ActorKind.Vehicle),
            (sim.SpawnInfantry(1, rifle, new CellCoord(25, 24), 0), ActorKind.Infantry),
            (sim.SpawnInfantry(1, rifle, new CellCoord(29, 25), 0), ActorKind.Infantry),
        };

        var attack = new List<Command>();
        ushort seq = 0;
        foreach (var (id, kind) in player0)
            attack.Add(new Command(AttackMoveTick, 0, seq++, CommandKind.AttackMove, id, EntityId.None, new CellCoord(24, 26), (int)kind));
        foreach (var (id, kind) in player1)
            attack.Add(new Command(AttackMoveTick, 1, seq++, CommandKind.AttackMove, id, EntityId.None, new CellCoord(6, 5), (int)kind));

        var lines = new List<string>();
        for (int step = 0; step < TotalTicks; step++)
        {
            sim.Step(sim.Tick == AttackMoveTick ? attack : Array.Empty<Command>());
            if (sim.Tick % HashInterval == 0)
            {
                lines.Add($"{sim.Tick} {sim.ComputeStateHash():x16}");
            }
        }
        return lines;
    }

    /// <summary>Path of the committed golden file in the source tree (found from the test binaries' folder).</summary>
    private static string GoldenPath()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Sim.Tests.csproj")))
        {
            dir = Path.GetDirectoryName(dir);
        }
        if (dir is null)
        {
            throw new InvalidOperationException($"Sim.Tests.csproj not found above {AppContext.BaseDirectory}.");
        }
        return Path.Combine(dir, "Golden", "golden_hashes.txt");
    }
}
