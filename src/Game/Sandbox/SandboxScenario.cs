// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Game.Sandbox;

/// <summary>
/// Spawns the starting armies for the debug view and records each spawn. Sandbox (default): per player, around its
/// start position, 4 Medium Tanks, 2 Jeeps, 1 more Medium Tank, 5 Riflemen in one cell (one per sub-slot)
/// and one Gun Turret if it can be placed. Empty (<c>--empty</c>): the legacy 3 tanks per player in a row.
/// Layouts run toward the map centre from each start, so they stay on the map.
/// </summary>
public static class SandboxScenario
{
    /// <summary>Map file name, looked up in data/maps.</summary>
    public const string MapFileName = "test_32.map.toml";

    private const string TankId = "auth_tank";
    private const string JeepId = "auth_jeep";
    private const string RifleId = "auth_rifleman";
    private const string TurretId = "auth_turret";
    private const int LegacyTanksPerPlayer = 3;
    private const int LegacySpacingCells = 2;

    /// <summary>Spawns the chosen setup and returns every unit spawned, in spawn order.</summary>
    public static List<OwnedUnit> Populate(Simulation sim, MapData map, SimRules rules, ReplayRecording recording, bool empty)
    {
        var army = new List<OwnedUnit>();
        if (empty) PopulateEmpty(sim, map, rules, recording, army);
        else PopulateSandbox(sim, map, rules, recording, army);
        return army;
    }

    private static void PopulateEmpty(Simulation sim, MapData map, SimRules rules, ReplayRecording rec, List<OwnedUnit> army)
    {
        int tank = Required(SimRules.IndexOf(rules.Vehicles, TankId, v => v.Id), TankId);
        for (byte player = 0; player < sim.Setup.PlayerCount; player++)
        {
            CellCoord start = map.StartPositions[player];
            int direction = player == 0 ? 1 : -1;
            for (int i = 0; i < LegacyTanksPerPlayer; i++)
            {
                Vehicle(sim, rec, army, player, tank, new CellCoord(start.X + direction * LegacySpacingCells * i, start.Y));
            }
        }
    }

    private static void PopulateSandbox(Simulation sim, MapData map, SimRules rules, ReplayRecording rec, List<OwnedUnit> army)
    {
        int tank = Required(SimRules.IndexOf(rules.Vehicles, TankId, v => v.Id), TankId);
        int jeep = Required(SimRules.IndexOf(rules.Vehicles, JeepId, v => v.Id), JeepId);
        int rifle = Required(SimRules.IndexOf(rules.Infantry, RifleId, i => i.Id), RifleId);
        int turret = Required(SimRules.IndexOf(rules.Buildings, TurretId, b => b.Id), TurretId);

        for (byte player = 0; player < sim.Setup.PlayerCount; player++)
        {
            CellCoord start = map.StartPositions[player];
            int dx = start.X < map.Width / 2 ? 1 : -1;
            int dy = start.Y < map.Height / 2 ? 1 : -1;
            CellCoord At(int along, int across) => new(start.X + dx * along, start.Y + dy * across);

            for (int i = 0; i < 4; i++) Vehicle(sim, rec, army, player, tank, At(2 * i, 0));
            for (int i = 0; i < 2; i++) Vehicle(sim, rec, army, player, jeep, At(2 * i, 2));
            Vehicle(sim, rec, army, player, tank, At(2, 4)); // (8,8) and (23,23) are cliff: keep vehicles off it
            for (int slot = 0; slot < InfantryStore.SubSlotCount; slot++) Infantry(sim, rec, army, player, rifle, At(0, 4), slot);
            Building(sim, rec, player, turret, At(6, 2));
        }
    }

    private static void Vehicle(Simulation sim, ReplayRecording rec, List<OwnedUnit> army, byte player, int type, CellCoord cell)
    {
        EntityId id = sim.SpawnVehicle(player, type, cell);
        army.Add(new OwnedUnit(new UnitKey(ActorKind.Vehicle, id), player));
    }

    private static void Infantry(Simulation sim, ReplayRecording rec, List<OwnedUnit> army, byte player, int type,
        CellCoord cell, int subSlot)
    {
        EntityId id = sim.SpawnInfantry(player, type, cell, subSlot);
        army.Add(new OwnedUnit(new UnitKey(ActorKind.Infantry, id), player));
    }

    // A building is spawned only when the sim says it can be placed; nothing is recorded otherwise.
    private static void Building(Simulation sim, ReplayRecording rec, byte player, int type, CellCoord topLeft)
    {
        if (sim.CanPlace(player, type, topLeft) != PlacementResult.Ok) return;
        sim.PlaceBuilding(player, type, topLeft);
    }

    private static int Required(int index, string id) =>
        index >= 0 ? index : throw new InvalidOperationException($"rules have no type '{id}' needed by the sandbox");
}
