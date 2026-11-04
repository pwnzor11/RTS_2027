// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Movement;
using Rts.Sim.Rules;
using Rts.Sim.Tunnels;

namespace Rts.Sim;

// Free burrowing, surfacing telegraphs, deploy-into-building and the building Upgrade command (design 05 §1, §4). OWNER: M3-03.
// State: Tunnels (layers, Telegraphs, BuildingMorphType/Ticks). Missions: MissionKind.Burrow / Surface / Deploy
// (Missions/BurrowMissions.cs). Burrowed travel, dive and surface cell rules: Simulation.BurrowMove.cs.
public sealed partial class Simulation
{
    /// <summary>Per tick (inside RunTunnels, after RunTunnelTransit): dive, burrowed travel, telegraph countdown, surfacing,
    /// Bore break-through, building morphs. Dive, travel and break-through start run in the mission handlers (RunMissions); this
    /// pass counts the telegraphs down, finishes surfacing, rescues burrowers whose mission was replaced, and finishes morphs.</summary>
    private void RunBurrowing()
    {
        RunTelegraphs();
        RunBurrowOrphans();
        RunBuildingMorphs();
    }

    // Telegraphs are created by the handlers in the same tick, before this pass, so a new one starts at ticks + 1 and the first
    // view of it shows the full duration; it is removed (and the unit appears) in the tick it reaches 0.
    private void RunTelegraphs()
    {
        List<Telegraph> list = _tunnels.Telegraphs;
        for (int t = 0; t < list.Count; t++)
        {
            Telegraph g = list[t];
            if (g.Kind == TelegraphKind.Unit && g.Subject.Kind == ActorKind.Vehicle)
            {
                if (!_units.Slots.IsAlive(g.Subject.Id)) { list.RemoveAt(t--); continue; }
                int i = g.Subject.Id.Index;
                if (_tunnels.VehicleLayer[i] != ActorLayer.Surfacing) continue;   // not ours
                g.TicksLeft--;
                if (g.TicksLeft > 0) { list[t] = g; continue; }
                list.RemoveAt(t--);
                FinishSurfacing(i, g.Cell);
            }
            else if (g.Kind == TelegraphKind.Entrance && g.Subject.Kind == ActorKind.Building)
            {
                if (!_buildings.Slots.IsAlive(g.Subject.Id)) { list.RemoveAt(t--); continue; }
                g.TicksLeft--;
                if (g.TicksLeft > 0) list[t] = g;
                else list.RemoveAt(t--);   // the entrance is finished: IsEmergingEntrance is false from now on
            }
        }
    }

    // A burrower that got another order (Stop, Attack, Guard, ...) while diving or burrowed cannot just carry on under that
    // mission: a diving unit climbs back out, a burrowed one surfaces where it is (Surface mission).
    private void RunBurrowOrphans()
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i)) continue;
            ActorLayer layer = _tunnels.VehicleLayer[i];
            if (layer != ActorLayer.Diving && layer != ActorLayer.Burrowed) continue;
            MissionKind m = _units.Mission[i];
            if (m == MissionKind.Burrow || m == MissionKind.Deploy || m == MissionKind.Surface || _units.NextMission[i] != MissionKind.None) continue;
            if (layer == ActorLayer.Diving) { _tunnels.VehicleLayer[i] = ActorLayer.Surface; ClearBurrowPath(i); }
            else { _units.Mission[i] = MissionKind.Surface; _units.Status[i] = MissionStatus.Init; _units.WakeTick[i] = 0; }
        }
    }

    private void RunBuildingMorphs()
    {
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!_buildings.Slots.IsIndexAlive(b) || _tunnels.BuildingMorphType[b] == 0) continue;
            if (--_tunnels.BuildingMorphTicks[b] > 0) continue;
            int target = _tunnels.BuildingMorphType[b] - 1;
            _tunnels.BuildingMorphType[b] = 0; _tunnels.BuildingMorphTicks[b] = 0;
            IReadOnlyList<BuildingType> types = RulesOf(_buildings.Player[b]).Buildings;
            if (target < 0 || target >= types.Count || _buildings.Health[b] <= 0) continue;
            BuildingType from = types[_buildings.TypeIndex[b]], to = types[target];
            _buildings.Health[b] = (int)Math.Max(1L, (long)_buildings.Health[b] * to.MaxHealth / Math.Max(1, from.MaxHealth));
            _emittersDirty = true;   // M3-INT
            _buildings.TypeIndex[b] = target;   // same footprint (checked by ApplyUpgrade): the map needs no change
        }
    }

    /// <summary>True while building slot <paramref name="buildingIndex"/> is a new entrance still behind its break-through
    /// telegraph (design 05 §1: not a network node until finished). M3-02 treats such a building as no entrance yet.</summary>
    internal bool IsEmergingEntrance(int buildingIndex)
    {
        List<Telegraph> list = _tunnels.Telegraphs;
        for (int t = 0; t < list.Count; t++)
            if (list[t].Kind == TelegraphKind.Entrance && list[t].Subject.Kind == ActorKind.Building && list[t].Subject.Id.Index == buildingIndex)
                return true;
        return false;
    }

    /// <summary>Deploy for a vehicle with DeploysInto &gt;= 0 (Bore: travel burrowed to Cell then break through into a Hatch;
    /// land-train: become the Command Shaft in place). Subject is an own live vehicle.</summary>
    private void ApplyDeployInto(Command c)
    {
        if (!_units.Slots.IsAlive(c.Subject) || _units.Player[c.Subject.Index] != c.Player) return;
        int i = c.Subject.Index;
        VehicleType vt = RulesOf(c.Player).Vehicles[_units.TypeIndex[i]];
        if (vt.DeploysInto < 0 || vt.DeploysInto >= RulesOf(c.Player).Buildings.Count || _units.Health[i] <= 0) return;
        ActorLayer layer = _tunnels.VehicleLayer[i];
        if (vt.Locomotion != Locomotion.Burrow)
        {
            // Land-train style: becomes the building where it stands. One-way (no re-pack): design 05 §4 gives no way back.
            if (layer == ActorLayer.Surface) TryDeployInPlace(i);
            return;
        }
        if (layer is not (ActorLayer.Surface or ActorLayer.Diving or ActorLayer.Burrowed) || !InMap(c.Cell)) return;
        if (layer != ActorLayer.Surface) { MoveMission.DropRequest(this, _units, i); ClearBurrowPath(i); }
        _units.Destination[i] = LeptonCoord.CenterOf(c.Cell);
        Queue(_units, i, MissionKind.Deploy, c.Cell, default);
    }

    /// <summary>Upgrade command: Subject = own building, Arg = target building type (Hatch to Bore Head; cost = difference).</summary>
    private void ApplyUpgrade(Command c)
    {
        if (!_buildings.Slots.IsAlive(c.Subject) || _buildings.Player[c.Subject.Index] != c.Player || _players.IsNeutral(c.Player)) return;
        int b = c.Subject.Index;
        SimRules rules = RulesOf(c.Player);
        int target = c.Arg;
        if (target < 0 || target >= rules.Buildings.Count || target == _buildings.TypeIndex[b] || _buildings.Health[b] <= 0) return;
        if (_tunnels.BuildingMorphType[b] != 0 || IsEmergingEntrance(b)) return;
        BuildingType from = rules.Buildings[_buildings.TypeIndex[b]], to = rules.Buildings[target];
        // The data names the target (buildings.toml morphs_into); the footprint must match so the map needs no change.
        if (from.MorphsInto != target || to.FootprintW != from.FootprintW || to.FootprintH != from.FootprintH) return;
        if (!HasPrerequisites(c.Player, to.Prerequisites)) return;
        int cost = Math.Max(0, to.Cost - from.Cost);
        if (_players.Cash[c.Player] < cost) return;
        _players.Cash[c.Player] -= cost;
        // ASSUMED (M3-03): without morph_ticks, the morph takes the build time of a unit costing the price difference (min 1).
        _tunnels.BuildingMorphType[b] = target + 1;
        _tunnels.BuildingMorphTicks[b] = from.MorphTicks > 0 ? from.MorphTicks : SimRules.BuildTicks(cost, 0, rules.Economy);
    }
}
