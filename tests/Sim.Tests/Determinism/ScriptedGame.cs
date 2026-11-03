using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim.Tests.Determinism;

/// <summary>Shared test fixture: a 2-player, 20-unit game and a seeded random command script.</summary>
public static class ScriptedGame
{
    public static VehicleType Vehicle(string id, int speed, int maxHealth) =>
        new(id, Locomotion.Tracked, speed, 8, maxHealth, ArmorClass.Heavy, 5, -1, false, 0, true, 0, Array.Empty<string>());

    public static InfantryType Infantry(string id, int speed, int maxHealth) =>
        new(id, speed, maxHealth, ArmorClass.Unarmored, 4, -1, 0, Array.Empty<string>());

    public static SimRules Rules(params VehicleType[] vehicles) => RulesWith(vehicles, Array.Empty<InfantryType>());

    public static SimRules RulesWith(VehicleType[] vehicles, InfantryType[] infantry) =>
        new(vehicles, infantry, Buildings(), Array.Empty<WeaponType>(),
            Array.Empty<WarheadType>(), Terrain(), Array.Empty<EffectDef>());

    /// <summary>Sorted by id: 0 = barracks (2x2), 1 = hq (3x3), 2 = turret (1x1).</summary>
    public static BuildingType[] Buildings() => new[]
    {
        Building("barracks", 2, 2, 300), Building("hq", 3, 3, 500), Building("turret", 1, 1, 100),
    };

    public static BuildingType Building(string id, int w, int h, int maxHealth) =>
        new(id, w, h, maxHealth, ArmorClass.Structure, 5, -1, 0, 0, Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Everything passable at 100% except Water, Rock and Cliff (impassable); only Clear and Road are buildable.</summary>
    public static TerrainTable Terrain()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        foreach (var land in new[] { LandType.Water, LandType.Rock, LandType.Cliff })
            for (int l = 0; l < RuleCounts.Locomotions; l++) speed[(int)land * RuleCounts.Locomotions + l] = 0;
        var buildable = new bool[RuleCounts.LandTypes];
        buildable[(int)LandType.Clear] = true; buildable[(int)LandType.Road] = true;
        return new TerrainTable(speed, buildable);
    }

    public static SimSetup Setup(ulong seed = 1) => new(seed, MapData.CreateFlat(64, 64), 2,
        RulesWith(new[] { Vehicle("fast", 37, 100), Vehicle("slow", 11, 200) },
            new[] { Infantry("rifle", 9, 50), Infantry("slowrifle", 5, 60) }), 0xABCDUL) { AllowDebug = true };

    public static IEnumerable<(byte Player, int Type, CellCoord Cell)> Spawns()
    {
        for (int i = 0; i < 20; i++)
            yield return ((byte)(i / 10), i % 2, new CellCoord(2 + i * 2, 2 + (i % 5)));
    }

    /// <summary>10 infantry (5 per player): sub-slots cycle 0..4 within shared cells.</summary>
    public static IEnumerable<(byte Player, int Type, CellCoord Cell, int SubSlot)> InfantrySpawns()
    {
        for (int i = 0; i < 10; i++)
            yield return ((byte)(i / 5), i % 2, new CellCoord(3 + i, 30 + (i / 5)), i % 5);
    }

    /// <summary>Two buildings placed at setup: (player, type index, top-left).</summary>
    public static IEnumerable<(byte Player, int Type, CellCoord Cell)> BuildingSpawns()
    {
        yield return (0, 1, new CellCoord(10, 45));
        yield return (1, 0, new CellCoord(50, 50));
    }

    public static Simulation NewSim(SimSetup setup)
    {
        var sim = new Simulation(setup);
        foreach (var s in Spawns()) sim.SpawnVehicle(s.Player, s.Type, s.Cell);
        foreach (var s in InfantrySpawns()) sim.SpawnInfantry(s.Player, s.Type, s.Cell, s.SubSlot);
        foreach (var s in BuildingSpawns()) sim.PlaceBuilding(s.Player, s.Type, s.Cell);
        return sim;
    }

    /// <summary>True for a Move of a vehicle the commanding player owns (so it is not ignored by the sim).</summary>
    public static bool IsEffectiveVehicleMove(Command c) =>
        c.Kind == CommandKind.Move && c.Arg == (int)ActorKind.Vehicle
        && c.Subject.Index >= 1 + c.Player * 10 && c.Subject.Index <= 10 + c.Player * 10;

    public static List<Command> Script(int ticks)
    {
        var rng = new SimRandom(7);
        var list = new List<Command>();
        ushort seq = 0;
        for (int t = 0; t < ticks; t++)
        {
            int n = rng.Next(0, 4);
            for (int k = 0; k < n; k++)
            {
                byte player = (byte)rng.Next(2);
                bool inf = rng.Next(3) == 0;
                int unit = inf ? 1 + player * 5 + rng.Next(5) : 1 + player * 10 + rng.Next(10);
                if (rng.Next(10) == 0) unit = inf ? 1 + (1 - player) * 5 + rng.Next(5) : 1 + (1 - player) * 10 + rng.Next(10); // not owned: ignored
                int kind = rng.Next(3);
                int arg = inf ? (int)ActorKind.Infantry : (int)ActorKind.Vehicle;
                var subj = new EntityId((ushort)unit, 1);
                var cell = new CellCoord(rng.Next(64), rng.Next(64));
                list.Add(kind switch
                {
                    0 => new Command(t, player, seq++, CommandKind.Move, subj, default, cell, arg),
                    1 => new Command(t, player, seq++, CommandKind.Stop, subj, default, default, arg),
                    _ => new Command(t, player, seq++, CommandKind.Debug, default, default, default, 1),
                });
            }
        }
        // Fixed Place/Sell commands appended without touching the RNG stream above. Setup buildings are ids 1 and 2;
        // placed ones get 3, 4, 5. The tick-120 Place lands on a cell the tick-5 building covers (rejected); the tick-150
        // Sell is issued by player 0 for its own hq.
        ushort pseq = 50000;
        void Add(int tick, byte player, CommandKind kind, EntityId subj, CellCoord cell, int arg) =>
            list.Add(new Command(tick, player, pseq++, kind, subj, default, cell, arg));
        if (ticks > 5) Add(5, 0, CommandKind.Place, default, new CellCoord(20, 20), 1);
        if (ticks > 30) Add(30, 1, CommandKind.Place, default, new CellCoord(40, 40), 0);
        if (ticks > 60) Add(60, 0, CommandKind.Place, default, new CellCoord(50, 10), 2);
        if (ticks > 120) Add(120, 1, CommandKind.Place, default, new CellCoord(21, 21), 2);
        if (ticks > 150) Add(150, 0, CommandKind.Sell, new EntityId(1, 1), default, (int)ActorKind.Building);
        if (ticks > 200) Add(200, 1, CommandKind.Place, default, new CellCoord(10, 45), 2);
        return list;
    }
}
