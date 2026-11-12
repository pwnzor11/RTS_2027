// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Economy;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.Sim;

// Money and power API shared by economy (M2-07, owner of this file) and production (M2-08, caller).
public sealed partial class Simulation
{
    /// <summary>Total spendable money of a player (Cash + Stored).</summary>
    public int Funds(byte player) => _players.Cash[player] + _players.Stored[player];

    /// <summary>
    /// Spend up to <paramref name="amount"/> credits: draws from Cash first, then Stored. Returns the amount actually
    /// spent (less than asked when broke — production drains cost per tick and stalls on a partial spend).
    /// CONTRACT STUB: M2-07 may refine (e.g. order of draw) but keeps the signature and the "returns spent" meaning.
    /// </summary>
    internal int Spend(byte player, int amount)
    {
        if (amount <= 0) return 0;
        int fromCash = Math.Min(amount, _players.Cash[player]);
        _players.Cash[player] -= fromCash;
        int fromStored = Math.Min(amount - fromCash, _players.Stored[player]);
        _players.Stored[player] -= fromStored;
        OnStatsSpent(player, fromCash + fromStored);   // M4-06
        return fromCash + fromStored;
    }

    /// <summary>Give credits back (cancel refunds, sell). Goes to Cash, which is uncapped.</summary>
    internal void Refund(byte player, int amount)
    {
        if (amount > 0) _players.Cash[player] += amount;
    }

    /// <summary>
    /// Power produced / used ratio band for production slowdown: 0 = full power, 1 = &lt;100 %, 2 = &lt;75 %, 3 = &lt;50 %.
    /// Reads Players.PowerOut/PowerUse, which M2-07 recomputes whenever buildings change.
    /// </summary>
    public int PowerBand(byte player)
    {
        int use = _players.PowerUse[player], output = _players.PowerOut[player];
        if (use <= output) return 0;
        long pct = output <= 0 ? 0 : (long)output * 100 / use;
        return pct >= 75 ? 1 : pct >= 50 ? 2 : 3;
    }

    /// <summary>
    /// Storage capacity of a player: the Storage of every own live building (refineries and silos). Stored credits above it
    /// are lost; unloading stops at it.
    /// </summary>
    public int StorageCap(byte player)
    {
        int cap = 0;
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player && _buildings.Health[i] > 0)
                cap += RulesOf(player).Buildings[_buildings.TypeIndex[i]].Storage;
        return cap;
    }

    /// <summary>Cargo of a harvester as per mille of its capacity (0 for anything that is not a live harvester). For the renderer.</summary>
    public int CargoPermille(EntityId vehicle)
    {
        if (!_units.Slots.IsAlive(vehicle)) return 0;
        int i = vehicle.Index, cap = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].HarvestCapacity;
        return cap <= 0 || _economy.Owner[i] != vehicle ? 0 : Math.Min(1000, _economy.Cargo[i] * 1000 / cap);
    }

    /// <summary>Harvest / Return commands (Subject = the player's own harvester). Harvest's Cell, when not (0,0), is the field to work near.</summary>
    private void ApplyEconomyCommand(Command c)
    {
        if (!_units.Slots.IsAlive(c.Subject) || _units.Player[c.Subject.Index] != c.Player) return;
        int i = c.Subject.Index;
        if (RulesOf(c.Player).Vehicles[_units.TypeIndex[i]].HarvestCapacity <= 0) return;
        _economy.Touch(i, c.Subject);
        if (c.Kind == CommandKind.Harvest)
        {
            if (c.Cell != default && InMap(c.Cell)) _economy.LastFieldCell[i] = c.Cell;
            Queue(_units, i, MissionKind.Harvest, default, default);
        }
        else Queue(_units, i, MissionKind.Return, default, default);
    }

    /// <summary>
    /// The economy system, once per tick after missions: harvesters that never got a mission start Harvest; dead harvesters' data
    /// is cleared; power totals and the storage cap are recomputed from the live buildings (Stored above the cap is lost);
    /// ashfall regrows (a slice of the map per tick, every cell once per RegrowthIntervalTicks).
    /// </summary>
    private void RunEconomy()
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i))
            {
                if (_economy.IsDirty(i)) _economy.Clear(i);
                continue;
            }
            if (_economy.NoRefinery[i] != 0)
            {   // D-205 (SIM-RULES-13): an idle harvester that gave up for lack of a refinery resumes once one exists
                if (_units.NextMission[i] != MissionKind.Guard && (_units.Mission[i] != MissionKind.Guard || _units.NextMission[i] != MissionKind.None)) _economy.NoRefinery[i] = 0;   // any other order cancels
                else if (_units.Mission[i] == MissionKind.Guard && _units.NextMission[i] == MissionKind.None && !HarvestMission.PickRefinery(this, _units.Player[i], _units.Position[i].ToCell()).IsNone)
                {
                    _economy.NoRefinery[i] = 0;
                    Queue(_units, i, _economy.Cargo[i] > 0 ? MissionKind.Return : MissionKind.Harvest, default, default);
                    continue;
                }
            }
            if (_units.Mission[i] != MissionKind.None || _units.NextMission[i] != MissionKind.None) continue;
            if (RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].HarvestCapacity <= 0) continue;
            _economy.Touch(i, _units.Slots.IdOf(i));
            Queue(_units, i, MissionKind.Harvest, default, default);
        }

        int n = _players.Count;
        Span<int> powerOut = stackalloc int[n], powerUse = stackalloc int[n], storage = stackalloc int[n];
        for (int i = 1; i < _buildings.Capacity; i++)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Health[i] <= 0) continue;
            byte p = _buildings.Player[i];
            BuildingType bt = RulesOf(p).Buildings[_buildings.TypeIndex[i]];
            if (bt.Power > 0) powerOut[p] += bt.Power; else powerUse[p] -= bt.Power;
            storage[p] += bt.Storage;
        }
        for (int p = 0; p < n; p++)
        {
            _players.PowerOut[p] = powerOut[p];
            _players.PowerUse[p] = powerUse[p];
            if (_players.Stored[p] > storage[p]) _players.Stored[p] = storage[p];
        }

        Regrow();
    }

    // Ashfall regrowth: tick t handles the cells whose orbit key (RegrowthSchedule) lies in [phase*N/interval, (phase+1)*N/interval)
    // with phase = t mod interval, so every cell is visited exactly once per interval and no cursor needs saving. Pitchblende and
    // empty cells never grow.
    private RegrowthSchedule? _regrowth;   // M4-16: derived from the map size only, not state (nothing to save or hash)

    private void Regrow()
    {
        int interval = Setup.Rules.Economy.RegrowthIntervalTicks;
        if (interval <= 0) return;
        int w = Setup.Map.Width, phase = Tick % interval;
        // M4-16: symmetric order (a cell and its mirror images regrow on the same tick); a row-major sweep favoured start 0
        _regrowth ??= new RegrowthSchedule(w, Setup.Map.Height);
        foreach (int k in _regrowth.Slice(phase, interval))
        {
            var c = new CellCoord(k % w, k / w);
            if (_map.OverlayAt(c) != OverlayKind.Ashfall) continue;
            int d = _map.DensityAt(c);
            if (d > 0 && d < EconomyState.MaxDensity) _map.SetOverlay(c, OverlayKind.Ashfall, (byte)(d + 1));
        }
    }
}
