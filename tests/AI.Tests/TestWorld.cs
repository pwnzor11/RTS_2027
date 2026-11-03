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
        new(new SimSetup(1, MapData.CreateFlat(size, size), players, Rules.Sim, Rules.RulesHash) { AllowDebug = true });

    private static ushort _seq = 60000;

    /// <summary>Spawn a vehicle after tick 0 through a Debug command (setup calls are rejected once the sim has
    /// stepped, M1.5-02). Costs one tick. Returns the new id.</summary>
    public static EntityId LateVehicle(Simulation sim, byte player, int type, CellCoord cell)
    {
        int slot = 1;
        while (sim.Units.Slots.IsIndexAlive(slot)) slot++;
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Debug, default, default, cell, (int)DebugOp.SpawnVehicle, type) });
        return sim.Units.Slots.IdOf(slot);
    }

    /// <summary>Place a building after tick 0 through a Place command. Costs one tick.</summary>
    public static EntityId LateBuilding(Simulation sim, byte player, int type, CellCoord topLeft)
    {
        sim.Step(new[] { new Command(sim.Tick, player, _seq++, CommandKind.Place, default, default, topLeft, type) });
        return sim.Map.BuildingAt(topLeft);
    }
}
