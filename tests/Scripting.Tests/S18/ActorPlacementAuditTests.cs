// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Scripting.Tests.S18;

/// <summary>
/// S18 MIS-13: a data audit of every mission's pre-placed actors (mission.toml [[actors]]): units standing on terrain their locomotion cannot
/// enter, inside a building footprint, or on a cell another vehicle already holds. Placement does not check any of this.
/// </summary>
public sealed class ActorPlacementAuditTests
{
    private static IEnumerable<string> MissionIds() =>
        Directory.GetDirectories(Path.Combine(TestData.DataDir, "missions")).Select(Path.GetFileName).Where(n => File.Exists(Path.Combine(TestData.DataDir, "missions", n!, "mission.toml"))).Order(StringComparer.Ordinal)!;

    /// <summary>One line per badly placed unit: "mission type (x,y) reason".</summary>
    private static List<string> Problems()
    {
        var found = new List<string>();
        foreach (string id in MissionIds())
        {
            MissionSession m = MissionSession.Create(TestData.DataDir, id);
            Simulation sim = m.Sim;
            SimRules rules = sim.Setup.Rules;
            for (int i = 1; i < sim.Units.Capacity; i++)
            {
                if (!sim.Units.Slots.IsIndexAlive(i)) continue;
                VehicleType vt = rules.Vehicles[sim.Units.TypeIndex[i]];
                CellCoord c = sim.Units.Position[i].ToCell();
                if (!sim.Map.IsPassableTerrain(c, vt.Locomotion, rules.Terrain)) found.Add($"{id} {vt.Id} ({c.X},{c.Y}) terrain");
                if (!sim.Map.BuildingAt(c).IsNone && vt.Locomotion != Locomotion.Air) found.Add($"{id} {vt.Id} ({c.X},{c.Y}) in building");
                EntityId holder = sim.Map.VehicleAt(c);
                if (!holder.IsNone && holder.Index != i && vt.Locomotion != Locomotion.Air) found.Add($"{id} {vt.Id} ({c.X},{c.Y}) cell held by another vehicle");
            }

            for (int i = 1; i < sim.Infantry.Capacity; i++)
            {
                if (!sim.Infantry.Slots.IsIndexAlive(i)) continue;
                string type = rules.Infantry![sim.Infantry.TypeIndex[i]].Id;
                CellCoord c = sim.Infantry.Position[i].ToCell();
                if (!sim.Map.IsPassableTerrain(c, Locomotion.Foot, rules.Terrain)) found.Add($"{id} {type} ({c.X},{c.Y}) terrain");
                if (!sim.Map.BuildingAt(c).IsNone) found.Add($"{id} {type} ({c.X},{c.Y}) in building");
            }
        }

        return found;
    }

    // S18 audit: pins current behaviour of MIS-13; flip the assertion when fixed (the list should be empty).
    [Fact]
    public void Audit_Pre_placed_actors_on_impassable_or_occupied_cells()
    {
        string[] expected =
        {
            "auth_01 auth_rifleman (55,6) terrain",          // svy1 stands on water
            "auth_04 dir_conscript (44,79) in building",     // inside a reactor vent
            "auth_04 dir_ashthrower (47,81) in building",    // inside the shaft
        };
        Assert.Equal(expected, Problems());
    }
}
