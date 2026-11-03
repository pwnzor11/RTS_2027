using System.Diagnostics;
using System.Globalization;
using Rts.Data;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Replay;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Tools;

/// <summary>
/// `Tools match`: a headless two-player skirmish. Each player gets a fixed start force at its start position; on
/// tick 1 every unit attack-moves toward the enemy start; the sim runs for a fixed number of ticks. Nothing else is
/// commanded. Exit codes: 0 ok, 1 error, 2 usage.
/// </summary>
public static class MatchCommands
{
    private const string Usage =
        "usage: match [--map <path>] [--ticks <n>] [--seed <s>] [--record <file>]\n" +
        "  --map defaults to data/maps/test_64.map.toml, --ticks to 2000, --seed to 1.";

    private const int DefaultTicks = 2000;
    private const ulong DefaultSeed = 1;
    private const int ReportIntervalTicks = 500;
    private const int AttackMoveTick = 1;
    private const int MinInfantryRing = 3, MaxInfantryRing = 8;
    private static readonly string[] Options = ["map", "ticks", "seed", "record"];

    // Start force per player: one entry per vehicle, then the infantry and the HQ.
    private static readonly string[] VehicleIds =
        ["test_tank", "test_tank", "test_tank", "test_tank", "test_jeep", "test_jeep", "test_artillery_truck"];
    private const string InfantryId = "test_rifleman";
    private const int InfantryPerPlayer = 6;
    private const string HqId = "test_hq";

    public static int Run(string[] args)
    {
        if (!MatchSetup.TryParse(args, Options, out var flags, out var positional, out string? error) || positional.Count > 0)
            return UsageError(error ?? $"unexpected argument '{positional[0]}'");

        if (!TryInt(flags, "ticks", DefaultTicks, out int ticks) || ticks < 0) return UsageError("--ticks must be a non-negative integer");
        if (!TryUlong(flags, "seed", DefaultSeed, out ulong seed)) return UsageError("--seed must be a non-negative integer");

        RuleSet? rules = MatchSetup.LoadRules(null, out string rulesDir);
        if (rules is null) return 1;
        string mapPath = flags.TryGetValue("map", out string? mapArg) ? mapArg : MatchSetup.DefaultMapPath(rulesDir);
        MapData? map = MatchSetup.LoadMap(mapPath);
        if (map is null) return 1;
        if (map.StartPositions.Count < MatchSetup.Players)
        {
            Console.Error.WriteLine($"error: map {map.Name} has {map.StartPositions.Count} start positions; {MatchSetup.Players} needed");
            return 1;
        }

        if (!TryFindTypes(rules, out int hq, out int rifle, out int[] vehicles, out string? missing))
        {
            Console.Error.WriteLine($"error: rules have no type '{missing}' needed by match");
            return 1;
        }

        SimSetup setup = MatchSetup.MakeSetup(seed, map, rules, MatchSetup.Players);
        var sim = new Simulation(setup);
        ReplayWriter? recorder = flags.ContainsKey("record") ? new ReplayWriter(setup) : null;

        var commands = new List<Command>();
        ushort sequence = 0;
        for (byte player = 0; player < MatchSetup.Players; player++)
        {
            CellCoord start = map.StartPositions[player];
            CellCoord enemy = map.StartPositions[(player + 1) % MatchSetup.Players];
            // Grow the force away from the nearest map edge so every cell stays on the map.
            int dx = start.X < map.Width / 2 ? 1 : -1, dy = start.Y < map.Height / 2 ? 1 : -1;

            for (int k = 0; k < vehicles.Length; k++)
            {
                var cell = new CellCoord(start.X + dx * k, start.Y);
                EntityId id = sim.SpawnVehicle(player, vehicles[k], cell);
                recorder?.RecordSpawn(ActorKind.Vehicle, player, vehicles[k], cell);
                Queue(commands, ref sequence, player, ActorKind.Vehicle, id, enemy);
            }
            for (int i = 0; i < InfantryPerPlayer; i++)
            {
                var cell = new CellCoord(start.X + dx * (i / InfantryStore.SubSlotCount), start.Y + 2 * dy);
                int subSlot = i % InfantryStore.SubSlotCount;
                EntityId id = sim.SpawnInfantry(player, rifle, cell, subSlot);
                recorder?.RecordSpawn(ActorKind.Infantry, player, rifle, cell, subSlot);
                Queue(commands, ref sequence, player, ActorKind.Infantry, id, enemy);
            }
            for (int ring = MinInfantryRing; ring <= MaxInfantryRing; ring++)
            {
                // Mirror the footprint, not just its anchor: on the far side, anchor from the footprint's far corner.
                BuildingType hqType = rules.Sim.Buildings[hq];
                int x0 = dx > 0 ? start.X + ring : start.X - ring - (hqType.FootprintW - 1);
                int y0 = dy > 0 ? start.Y + ring : start.Y - ring - (hqType.FootprintH - 1);
                var topLeft = new CellCoord(x0, y0);
                if (sim.PlaceBuilding(player, hq, topLeft) == EntityId.None) continue;
                recorder?.RecordSpawn(ActorKind.Building, player, hq, topLeft);
                break;
            }
        }

        if (recorder is not null) foreach (Command c in commands) recorder.RecordCommand(c);

        var clock = Stopwatch.StartNew();
        for (int t = 0; t < ticks; t++)
        {
            sim.Step(commands);
            if (recorder is not null && sim.Tick % SimConstants.HashIntervalTicks == 0)
                recorder.RecordHash(sim.Tick, sim.ComputeStateHash(), sim.ComputeSubHashes());
            if (sim.Tick % ReportIntervalTicks == 0)
                Console.WriteLine($"tick {sim.Tick} alive p0 {Alive(sim, 0)} p1 {Alive(sim, 1)} hash 0x{sim.ComputeStateHash():x16}");
        }
        clock.Stop();

        double seconds = Math.Max(clock.Elapsed.TotalSeconds, 1e-9);
        Console.WriteLine($"ran {sim.Tick} ticks in {seconds:0.00}s ({(long)(sim.Tick / seconds)} ticks/second)");

        if (recorder is null) return 0;
        try
        {
            string path = flags["record"];
            string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var fs = File.Create(path)) recorder.Finish(fs, sim.Tick);
            Console.WriteLine($"recorded {path}");
            return 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    /// <summary>Queues an attack-move toward the enemy start for one unit, applied on tick 1.</summary>
    private static void Queue(List<Command> commands, ref ushort sequence, byte player, ActorKind kind, EntityId id, CellCoord enemy)
    {
        commands.Add(new Command(AttackMoveTick, player, sequence++, CommandKind.AttackMove, id, EntityId.None, enemy, (int)kind));
    }

    /// <summary>Living vehicles and infantry owned by <paramref name="player"/>.</summary>
    private static int Alive(Simulation sim, byte player)
    {
        int n = 0;
        for (int i = 1; i < sim.Units.Capacity; i++)
            if (sim.Units.Slots.IsIndexAlive(i) && sim.Units.Player[i] == player) n++;
        for (int i = 1; i < sim.Infantry.Capacity; i++)
            if (sim.Infantry.Slots.IsIndexAlive(i) && sim.Infantry.Player[i] == player) n++;
        return n;
    }

    private static bool TryFindTypes(RuleSet rules, out int hq, out int rifle, out int[] vehicles, out string? missing)
    {
        missing = null;
        vehicles = new int[VehicleIds.Length];
        for (int k = 0; k < VehicleIds.Length; k++)
        {
            vehicles[k] = SimRules.IndexOf(rules.Sim.Vehicles, VehicleIds[k], v => v.Id);
            if (vehicles[k] < 0) { missing = VehicleIds[k]; hq = rifle = -1; return false; }
        }
        rifle = SimRules.IndexOf(rules.Sim.Infantry, InfantryId, i => i.Id);
        if (rifle < 0) { missing = InfantryId; hq = -1; return false; }
        hq = SimRules.IndexOf(rules.Sim.Buildings, HqId, b => b.Id);
        if (hq < 0) { missing = HqId; return false; }
        return true;
    }

    private static bool TryInt(Dictionary<string, string> flags, string name, int fallback, out int value)
    {
        value = fallback;
        return !flags.TryGetValue(name, out string? text) || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryUlong(Dictionary<string, string> flags, string name, ulong fallback, out ulong value)
    {
        value = fallback;
        return !flags.TryGetValue(name, out string? text) || ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static int UsageError(string message)
    {
        Console.Error.WriteLine($"error: {message}");
        Console.Error.WriteLine(Usage);
        return 2;
    }
}
