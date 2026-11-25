// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// S44 M-03 / M-12. No state of its own: every effect is a pure function of the tick and the world, so nothing needs hashing or saving.
//  * DRYDOCK (BuildingType.ShipRepairPermille > 0): every RepairPeriod ticks each alive own/allied ship (naval vehicle, surface layer) within
//    ShipRepairRadius cells of the footprint heals max(1, MaxHealth * permille / 1000), never above MaxHealth. The ship's owner pays
//    heal * Economy.RepairCostPermille * Cost / (MaxHealth * 1000) credits, rounded up; a ship whose owner cannot pay is skipped that period.
//    The best (highest-rate) yard in reach is used alone: yards do not stack. Production at the yard (BuiltAt) is in Simulation.Production.cs.
//  * BAY REPAIR (VehicleType.BayRepairPermille > 0): every RepairPeriod ticks each passenger (soldier or vehicle) inside such a carrier heals
//    max(1, MaxHealth * permille / 1000). Free of charge. A passenger inside a dying carrier is handled by the transport code as before.
public sealed partial class Simulation
{
    /// <summary>Ticks between two heals of the drydock and the bay (the same cadence as the mender aura: rates are permille per 10 ticks).</summary>
    internal const int DrydockRepairPeriod = 10;

    /// <summary>M-03 drydock repair, M-12 bay repair.</summary>
    private void RunDrydocks()
    {
        if (Tick % DrydockRepairPeriod != 0) return;
        RunDrydockRepair();
        RunBayRepair();
    }

    private void RunDrydockRepair()
    {
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _units.Health[v] <= 0 || _tunnels.VehicleLayer[v] != ActorLayer.Surface) continue;
            byte owner = _units.Player[v];
            VehicleType vt = RulesOf(owner).Vehicles[_units.TypeIndex[v]];
            if (vt.Locomotion != Locomotion.Naval || _units.Health[v] >= vt.MaxHealth || vt.MaxHealth <= 0) continue;
            int rate = BestYardRate(v, owner);
            if (rate <= 0) continue;
            int heal = Math.Min(vt.MaxHealth - _units.Health[v], Math.Max(1, (int)((long)vt.MaxHealth * rate / 1000)));
            long den = (long)vt.MaxHealth * 1000;
            int credits = (int)(((long)heal * RulesOf(owner).Economy.RepairCostPermille * vt.Cost + den - 1) / den);
            if (credits > Funds(owner)) continue;
            Spend(owner, credits);
            _units.Health[v] += heal;
        }
    }

    /// <summary>The highest ShipRepairPermille among alive, unpacked friendly yards whose footprint (grown by ShipRepairRadius) holds the ship's cell.</summary>
    private int BestYardRate(int v, byte owner)
    {
        CellCoord c = _units.Position[v].ToCell();
        int best = 0;
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!_buildings.Slots.IsIndexAlive(b) || _buildings.Health[b] <= 0 || _rigs.PackTicksLeft[b] != 0) continue;
            BuildingType bt = BuildingTypeAt(b);
            if (bt.ShipRepairPermille <= best || !IsFriendly(_buildings.Player[b], owner)) continue;
            CellCoord tl = _buildings.Cell[b];
            int r = bt.ShipRepairRadius;
            if (c.X < tl.X - r || c.X > tl.X + bt.FootprintW - 1 + r || c.Y < tl.Y - r || c.Y > tl.Y + bt.FootprintH - 1 + r) continue;
            best = bt.ShipRepairPermille;
        }
        return best;
    }

    private void RunBayRepair()
    {
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _tunnels.InfantryLayer[i] != ActorLayer.Passenger || _infantry.Health[i] <= 0) continue;
            int rate = BayRate(_tunnels.InfantryCarrier[i]);
            if (rate <= 0) continue;
            int max = RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]].MaxHealth;
            if (_infantry.Health[i] < max) _infantry.Health[i] = Math.Min(max, _infantry.Health[i] + Math.Max(1, (int)((long)max * rate / 1000)));
        }
        for (int v = 1; v < _units.Capacity; v++)
        {
            if (!_units.Slots.IsIndexAlive(v) || _tunnels.VehicleLayer[v] != ActorLayer.Passenger || _units.Health[v] <= 0) continue;
            int rate = BayRate(_tunnels.VehicleCarrier[v]);
            if (rate <= 0) continue;
            int max = RulesOf(_units.Player[v]).Vehicles[_units.TypeIndex[v]].MaxHealth;
            if (_units.Health[v] < max) _units.Health[v] = Math.Min(max, _units.Health[v] + Math.Max(1, (int)((long)max * rate / 1000)));
        }
    }

    /// <summary>BayRepairPermille of an alive carrier vehicle, else 0 (a hangar-docked aircraft has no VehicleCarrier and is left to the carrier code).</summary>
    private int BayRate(EntityId carrier)
    {
        if (carrier.IsNone || !_units.Slots.IsAlive(carrier) || _units.Health[carrier.Index] <= 0) return 0;
        return RulesOf(_units.Player[carrier.Index]).Vehicles[_units.TypeIndex[carrier.Index]].BayRepairPermille;
    }
}
