// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Garrison;
using Rts.Sim.Rules;

namespace Rts.Sim;

// M4-17 (D-173): skirmish "no way to rebuild" defeat. A player who owns no production structure, no unit that can found one and no
// harvester for SimConstants.NoRebuildDefeatTicks consecutive ticks is defeated, whatever else is left. The clock is ProductionState.NoRebuildTicks.
// Only runs when SimSetup.NoRebuildDefeat is on (skirmish and AI matches; missions leave it off). Everything is derived from the rules:
//  * production structure: a building type with Produces set (infantry/vehicle/aircraft lanes), or a tech-tree root (no prerequisites
//    itself, and another building type lists its id or provides name as a prerequisite): the conyard-type building that opens the
//    structure queue (auth_conyard, dir_shaft, lb_mother_rig, exp_landing_depot in the shipped data);
//  * founder: a vehicle whose DeploysInto / UnpacksInto (a packed rig: the type it was packed from) is such a building type;
//  * harvester: a vehicle with HarvestCapacity > 0.
public sealed partial class Simulation
{
    private bool[]? _lifelineBuilding;   // lazy rules-derived cache, not state
    private bool[]? _lifelineVehicle;    // lazy rules-derived cache (founder or harvester), not state

    private void EnsureLifelineTypes()
    {
        if (_lifelineBuilding != null) return;
        SimRules rules = Setup.Rules;
        int nb = rules.Buildings.Count;
        var root = new bool[nb];
        for (int b = 0; b < nb; b++)
        {
            BuildingType bt = rules.Buildings[b];
            if (bt.Produces != null) { root[b] = true; continue; }
            if (bt.Prerequisites.Count != 0) continue;
            for (int o = 0; o < nb && !root[b]; o++)
            {
                if (o == b) continue;
                IReadOnlyList<string> pre = rules.Buildings[o].Prerequisites;
                for (int k = 0; k < pre.Count; k++)
                {
                    if (string.Equals(pre[k], bt.Id, StringComparison.Ordinal)) { root[b] = true; break; }
                    bool provided = false;
                    for (int q = 0; q < bt.Provides.Count; q++)
                        if (string.Equals(pre[k], bt.Provides[q], StringComparison.Ordinal)) { provided = true; break; }
                    if (provided) { root[b] = true; break; }
                }
            }
        }
        int nv = rules.Vehicles.Count;
        var veh = new bool[nv];
        for (int v = 0; v < nv; v++)
        {
            VehicleType vt = rules.Vehicles[v];
            veh[v] = vt.HarvestCapacity > 0
                || (vt.DeploysInto >= 0 && vt.DeploysInto < nb && root[vt.DeploysInto])
                || (vt.UnpacksInto >= 0 && vt.UnpacksInto < nb && root[vt.UnpacksInto]);
        }
        _lifelineVehicle = veh;
        _lifelineBuilding = root;
    }

    /// <summary>True if <paramref name="player"/> owns a production structure, a founder or a harvester (alive; a flipped ruin does not count).</summary>
    private bool HasRebuildLifeline(byte player)
    {
        EnsureLifelineTypes();
        bool[] bld = _lifelineBuilding!, veh = _lifelineVehicle!;
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player && _buildings.Health[i] > 0
                && _garrison.HomeOwner[i] == GarrisonState.NoOwner && bld[_buildings.TypeIndex[i]]) return true;
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _units.Player[v] != player || _units.Health[v] <= 0) continue;
            int t = _units.TypeIndex[v];
            if (_rigs.IsPackedRig(v))
            {
                int into = UnpackTypeOf(v);   // a packed rig founds the type it was packed from
                if (into >= 0 && into < bld.Length && bld[into]) return true;
            }
            else if (veh[t]) return true;
        }
        return false;
    }

    /// <summary>Per tick, after the old defeat rule: advances or resets each live player's clock and defeats at the limit.</summary>
    private void UpdateNoRebuildDefeat()
    {
        if (!Setup.NoRebuildDefeat) return;
        for (int p = 0; p < _players.Count; p++)
        {
            if (_players.IsNeutral((byte)p) || _players.Defeated[p] || !_production.EverHadBase[p]) continue;
            if (HasRebuildLifeline((byte)p)) { _production.NoRebuildTicks[p] = 0; continue; }
            if (++_production.NoRebuildTicks[p] >= SimConstants.NoRebuildDefeatTicks) _players.Defeated[p] = true;
        }
    }
}
