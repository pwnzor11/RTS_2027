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
        new(vehicles, infantry, Array.Empty<BuildingType>(), Array.Empty<WeaponType>(),
            Array.Empty<WarheadType>(), Flat100(), Array.Empty<EffectDef>());

    private static TerrainTable Flat100()
    {
        var speed = new int[RuleCounts.LandTypes * RuleCounts.Locomotions];
        Array.Fill(speed, 100);
        return new TerrainTable(speed, new bool[RuleCounts.LandTypes]);
    }

    public static SimSetup Setup(ulong seed = 1) => new(seed, MapData.CreateFlat(64, 64), 2,
        RulesWith(new[] { Vehicle("fast", 37, 100), Vehicle("slow", 11, 200) },
            new[] { Infantry("rifle", 9, 50), Infantry("slowrifle", 5, 60) }), 0xABCDUL);

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

    public static Simulation NewSim(SimSetup setup)
    {
        var sim = new Simulation(setup);
        foreach (var s in Spawns()) sim.SpawnVehicle(s.Player, s.Type, s.Cell);
        foreach (var s in InfantrySpawns()) sim.SpawnInfantry(s.Player, s.Type, s.Cell, s.SubSlot);
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
        return list;
    }
}
