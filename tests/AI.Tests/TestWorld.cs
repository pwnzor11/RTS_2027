using Rts.AI;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace AI.Tests;

/// <summary>Real rules and difficulty data, plus small simulation builders.</summary>
internal static class TestWorld
{
    public static readonly string RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory)!;
    public static readonly RuleSet Rules = RulesLoader.Load(RulesDir);
    public static readonly IReadOnlyList<DifficultyProfile> Tiers = DifficultyLoader.Load(DifficultyLoader.PathBesideRules(RulesDir));

    public static int Vehicle(string id) => Array.FindIndex(Rules.Sim.Vehicles.ToArray(), v => v.Id == id);

    public static Simulation FlatSim(int players = 2, int size = 64) =>
        new(new SimSetup(1, MapData.CreateFlat(size, size), players, Rules.Sim, Rules.RulesHash));
}
