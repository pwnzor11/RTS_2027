// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Mines;
using Rts.Sim.Missions;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// S44 M-01 (BACKLOG M-01; design 07_expedition.md: "minelayer lays sea mines, sonar, mine-sweeping"). Sea mines.
//  * Lay: Command Deploy on a minelayer (VehicleType.MineCapacity > 0). Arg2 = 1: lay on Command.Cell (the ship sails there first and
//    lays on arrival; refused for a cell that is not sea or not reachable); Arg2 = 0: lay on the ship's own cell. One mine per
//    mine_lay_ticks, at most mine_capacity before a restock. The old Deploy encodings are unchanged.
//  * Trigger (RunMines, after movement): an enemy surface ship on a mine's cell (within the mine's trigger radius) sets it off: one hit of
//    mine_damage through the warhead table (Hurt), the mine is cleared. Own and allied ships never trigger it. A dived submarine passes under.
//  * Sweep: a minelayer with mine_sweep_radius clears every enemy mine in that radius every mine_sweep_ticks (automatic).
//  * Restock: next to a friendly WaterBuilding (a shipyard) a minelayer regains one mine per mine_restock_ticks.
//  * Seen: own and allied mines always; an enemy mine only while a sonar (ship or picket of the viewer's team) covers its cell.
// ASSUMED (S44): capacity 6, damage 250, lay interval 30 ticks, sweep radius 3 / 60 ticks, restock 90 ticks (all in data/rules/vehicles.toml).
public sealed partial class Simulation
{
    /// <summary>Restocking needs the minelayer within this many cells (Chebyshev, to the footprint) of a friendly water building.</summary>
    internal const int MineRestockReach = 2;

    /// <summary>The "mines" part (S44 M-01): the mine table and the minelayers' stock.</summary>
    public MineState Mines => _mines;

    /// <summary>Mines still aboard the vehicle (mine_capacity minus the ones laid since the last restock); 0 for a vehicle that is not a minelayer.</summary>
    public int MineStockOf(EntityId vehicle)
    {
        if (!_units.Slots.IsAlive(vehicle)) return 0;
        int i = vehicle.Index;
        int cap = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].MineCapacity;
        return Math.Max(0, cap - _mines.Laid[i]);
    }

    /// <summary>True if the mine slot is live and <paramref name="viewer"/> may see it (own team, or covered by the viewer's sonar).</summary>
    public bool MineVisibleTo(byte viewer, int slot)
    {
        MineState m = _mines;
        if (slot <= 0 || slot >= m.Live.Length || !m.Live[slot]) return false;
        if (IsFriendly(viewer, m.Owner[slot])) return true;
        return IsSonarRevealed(viewer, new CellCoord(m.CellX[slot], m.CellY[slot]));
    }

    /// <summary>Lowest live mine slot on <paramref name="cell"/>, or 0.</summary>
    public int FindMineAt(CellCoord cell)
    {
        MineState m = _mines;
        for (int s = 1; s < m.Live.Length; s++)
            if (m.Live[s] && m.CellX[s] == cell.X && m.CellY[s] == cell.Y) return s;
        return 0;
    }

    /// <summary>Deploy on a minelayer (payload above). Refusals leave the ship's orders untouched.</summary>
    private void ApplyLayMine(Command c)
    {
        int i = c.Subject.Index;
        if (!_units.Slots.IsAlive(c.Subject) || _units.Player[i] != c.Player || _units.Health[i] <= 0) return;
        if (RulesOf(c.Player).Vehicles[_units.TypeIndex[i]].MineCapacity - _mines.Laid[i] <= 0) return;   // nothing aboard
        CellCoord here = _units.Position[i].ToCell();
        CellCoord target = c.Arg2 == 1 ? c.Cell : here;
        if (!InMap(target) || NavalRegionAt(target, false) == 0) return;   // sea cells only
        if (here != target)
        {
            if (MoveGoalRefused(ActorKind.Vehicle, _units, i, target)) return;
            ApplyMovementCommand(new Command(c.ExecTick, c.Player, c.Sequence, CommandKind.Move, c.Subject, default, target, (int)ActorKind.Vehicle));
            if (_units.NextMission[i] != MissionKind.Move || _units.NextCell[i] != target) return;
        }
        _mines.OrderSet[i] = true; _mines.OrderX[i] = target.X; _mines.OrderY[i] = target.Y;
    }

    /// <summary>Per tick after movement: minelayer timers and lay orders, sweeping, then the mine triggers (slot order throughout).</summary>
    private void RunMines()
    {
        MineState m = _mines;
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _units.Health[i] <= 0) continue;
            VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
            if (vt.MineCapacity <= 0 && vt.MineSweepRadius <= 0) continue;
            CellCoord cur = _units.Position[i].ToCell();
            if (m.Cooldown[i] > 0) m.Cooldown[i]--;
            if (vt.MineCapacity > 0)
            {
                RestockMines(i, vt, cur);
                if (m.OrderSet[i]) StepLayOrder(i, vt, cur);
            }
            if (vt.MineSweepRadius > 0 && vt.MineSweepTicks > 0 && Tick % vt.MineSweepTicks == 0) SweepMines(_units.Player[i], cur, vt.MineSweepRadius);
        }
        if (m.Count == 0) return;
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _units.Health[i] <= 0 || !IsNavalShip(ActorKind.Vehicle, i) || IsHiddenSub(ActorKind.Vehicle, i)) continue;
            CellCoord cell = _units.Position[i].ToCell();
            byte owner = _units.Player[i];
            for (int s = 1; s < m.Live.Length; s++)
            {
                if (!m.Live[s] || RelationOf(m.Owner[s], owner) != Relation.Enemy) continue;
                if (LeptonMath.ChebyshevCells(cell, new CellCoord(m.CellX[s], m.CellY[s])) > m.Radius[s]) continue;
                IReadOnlyList<WarheadType> list = RulesOf(m.Owner[s]).Warheads;
                if ((uint)m.Warhead[s] < (uint)list.Count) Hurt(ActorKind.Vehicle, i, m.Damage[s], list[m.Warhead[s]], false, m.Owner[s]);
                _fx.Add(new FxEvent(FxKind.Detonation, LeptonCoord.CenterOf(new CellCoord(m.CellX[s], m.CellY[s])), 1));
                m.Clear(s);
                break;   // one mine per ship per tick
            }
        }
    }

    /// <summary>One tick of a pending lay order: lay on arrival (when the cooldown allows), drop the order when the ship went elsewhere.</summary>
    private void StepLayOrder(int i, VehicleType vt, CellCoord cur)
    {
        MineState m = _mines;
        var target = new CellCoord(m.OrderX[i], m.OrderY[i]);
        if (cur == target)
        {
            if (vt.MineCapacity - m.Laid[i] <= 0) { ClearLayOrder(i); return; }
            if (m.Cooldown[i] > 0) return;   // wait for the previous mine's interval
            if (LayMineAt(i, vt, target)) m.Cooldown[i] = Math.Max(0, vt.MineLayTicks);
            ClearLayOrder(i);
            return;
        }
        bool moving = _units.Mission[i] == MissionKind.Move || _units.NextMission[i] == MissionKind.Move;
        if (!moving || (_units.PathActive[i] && _units.PathGoal[i] != target)) ClearLayOrder(i);   // stopped, blocked or re-ordered elsewhere
    }

    private void ClearLayOrder(int i) { _mines.OrderSet[i] = false; _mines.OrderX[i] = 0; _mines.OrderY[i] = 0; }

    /// <summary>Puts one mine of ship <paramref name="i"/> on <paramref name="cell"/>: false when the table is full or the cell already holds a mine.</summary>
    private bool LayMineAt(int i, VehicleType vt, CellCoord cell)
    {
        MineState m = _mines;
        if (FindMineAt(cell) != 0) return false;
        int slot = 0;
        for (int s = 1; s < m.Live.Length; s++) if (!m.Live[s]) { slot = s; break; }
        if (slot == 0) return false;
        byte owner = _units.Player[i];
        IReadOnlyList<WarheadType> list = RulesOf(owner).Warheads;
        int wh = -1;
        for (int n = 0; n < list.Count; n++) if (string.Equals(list[n].Id, vt.MineWarhead, StringComparison.Ordinal)) { wh = n; break; }
        if (wh < 0) return false;
        m.Live[slot] = true; m.Owner[slot] = owner; m.CellX[slot] = cell.X; m.CellY[slot] = cell.Y;
        m.Damage[slot] = vt.MineDamage; m.Radius[slot] = vt.MineTriggerRadius; m.Warhead[slot] = wh;
        m.Laid[i]++;
        return true;
    }

    /// <summary>Restock: beside a friendly water building one mine returns every mine_restock_ticks.</summary>
    private void RestockMines(int i, VehicleType vt, CellCoord cur)
    {
        MineState m = _mines;
        if (m.Laid[i] <= 0 || vt.MineRestockTicks <= 0) { m.Restock[i] = 0; return; }
        if (!NearOwnWaterBuilding(_units.Player[i], cur)) return;
        if (++m.Restock[i] < vt.MineRestockTicks) return;
        m.Restock[i] = 0; m.Laid[i]--;
    }

    private bool NearOwnWaterBuilding(byte player, CellCoord cell)
    {
        for (int b = 1; b < _buildings.Capacity; b++)
        {
            if (!_buildings.Slots.IsIndexAlive(b) || _buildings.Health[b] <= 0 || !IsFriendly(player, _buildings.Player[b])) continue;
            BuildingType bt = RulesOf(_buildings.Player[b]).Buildings[_buildings.TypeIndex[b]];
            if (!bt.WaterBuilding) continue;
            CellCoord tl = _buildings.Cell[b];
            int dx = Math.Max(0, Math.Max(tl.X - cell.X, cell.X - (tl.X + bt.FootprintW - 1)));
            int dy = Math.Max(0, Math.Max(tl.Y - cell.Y, cell.Y - (tl.Y + bt.FootprintH - 1)));
            if (Math.Max(dx, dy) <= MineRestockReach) return true;
        }
        return false;
    }

    /// <summary>Clears every mine not owned by a friend of <paramref name="player"/> within <paramref name="radius"/> cells of <paramref name="centre"/>.</summary>
    private void SweepMines(byte player, CellCoord centre, int radius)
    {
        MineState m = _mines;
        for (int s = 1; s < m.Live.Length; s++)
        {
            if (!m.Live[s] || IsFriendly(player, m.Owner[s])) continue;
            if (LeptonMath.ChebyshevCells(centre, new CellCoord(m.CellX[s], m.CellY[s])) <= radius) m.Clear(s);
        }
    }
}
