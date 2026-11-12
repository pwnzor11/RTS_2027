// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Combat;

/// <summary>
/// State part "combat": a <see cref="CombatEntry"/> per slot for vehicles, infantry and buildings (index = slot).
/// Serialisation: for each kind (vehicle, infantry, building) the capacity, then per slot 1..cap-1 a flag byte
/// (0 = default entry) followed, if set, by the entry fields in declaration order.
/// S19 (M13-15): flag 2 = the entry fields as for 1, then the slot's orders data (stance byte, waypoint count, attack-move mask, eight
/// waypoint cells). Vehicles and infantry only. A slot with default stance and an empty queue writes flag 0 or 1 exactly as before, so
/// games that use neither keep their bytes and hashes.
/// </summary>
public sealed class CombatState : ISimStatePart
{
    private readonly CombatEntry[][] _entries;

    /// <summary>Waypoints a unit can queue (S19 M13-15).</summary>
    public const int MaxWaypoints = 8;

    // S19 (M13-15): per mobile slot (kind 0 = vehicle, 1 = infantry). _wpCell is laid out [k * capacity + slot].
    private readonly byte[][] _stance;
    private readonly byte[][] _wpCount;
    private readonly byte[][] _wpAttack;   // bit k set = waypoint k is an attack-move
    private readonly CellCoord[][] _wpCell;

    public CombatState(int vehicleCapacity = UnitStore.DefaultCapacity, int infantryCapacity = InfantryStore.DefaultCapacity,
        int buildingCapacity = BuildingStore.DefaultCapacity)
    {
        _entries = new[]
        {
            new CombatEntry[vehicleCapacity], new CombatEntry[infantryCapacity], new CombatEntry[buildingCapacity],
        };
        _stance = new[] { new byte[vehicleCapacity], new byte[infantryCapacity] };
        _wpCount = new[] { new byte[vehicleCapacity], new byte[infantryCapacity] };
        _wpAttack = new[] { new byte[vehicleCapacity], new byte[infantryCapacity] };
        _wpCell = new[] { new CellCoord[vehicleCapacity * MaxWaypoints], new CellCoord[infantryCapacity * MaxWaypoints] };
    }

    /// <summary>Stance of a vehicle or infantry slot (0 = aggressive); other kinds are always 0.</summary>
    public UnitStance StanceOf(ActorKind kind, int index) => (int)kind < 2 ? (UnitStance)_stance[(int)kind][index] : UnitStance.Aggressive;

    /// <summary>Sets the stance of a vehicle or infantry slot.</summary>
    public void SetStance(ActorKind kind, int index, UnitStance stance)
    {
        if ((int)kind < 2) _stance[(int)kind][index] = (byte)stance;
    }

    /// <summary>Queued waypoints of the slot (0 for other kinds).</summary>
    public int WaypointCount(ActorKind kind, int index) => (int)kind < 2 ? _wpCount[(int)kind][index] : 0;

    /// <summary>Waypoint <paramref name="k"/> (0 = next to run) of the slot; false when there is none.</summary>
    public bool TryGetWaypoint(ActorKind kind, int index, int k, out CellCoord cell, out bool attackMove)
    {
        cell = default; attackMove = false;
        if ((int)kind >= 2 || k < 0 || k >= _wpCount[(int)kind][index]) return false;
        cell = _wpCell[(int)kind][k * _stance[(int)kind].Length + index];
        attackMove = (_wpAttack[(int)kind][index] & (1 << k)) != 0;
        return true;
    }

    /// <summary>Appends a waypoint; false when the queue is full or the kind has no queue.</summary>
    public bool AddWaypoint(ActorKind kind, int index, CellCoord cell, bool attackMove)
    {
        if ((int)kind >= 2) return false;
        int n = _wpCount[(int)kind][index];
        if (n >= MaxWaypoints) return false;
        _wpCell[(int)kind][n * _stance[(int)kind].Length + index] = cell;
        if (attackMove) _wpAttack[(int)kind][index] |= (byte)(1 << n);
        _wpCount[(int)kind][index] = (byte)(n + 1);
        return true;
    }

    /// <summary>Removes and returns the first waypoint; false when the queue is empty.</summary>
    public bool PopWaypoint(ActorKind kind, int index, out CellCoord cell, out bool attackMove)
    {
        if (!TryGetWaypoint(kind, index, 0, out cell, out attackMove)) return false;
        int cap = _stance[(int)kind].Length, n = _wpCount[(int)kind][index];
        CellCoord[] cells = _wpCell[(int)kind];
        for (int k = 1; k < n; k++) cells[(k - 1) * cap + index] = cells[k * cap + index];
        cells[(n - 1) * cap + index] = default;
        _wpAttack[(int)kind][index] >>= 1;
        _wpCount[(int)kind][index] = (byte)(n - 1);
        return true;
    }

    /// <summary>Empties the waypoint queue of the slot.</summary>
    public void ClearWaypoints(ActorKind kind, int index)
    {
        if ((int)kind >= 2 || _wpCount[(int)kind][index] == 0) return;
        int cap = _stance[(int)kind].Length;
        for (int k = 0; k < MaxWaypoints; k++) _wpCell[(int)kind][k * cap + index] = default;
        _wpAttack[(int)kind][index] = 0;
        _wpCount[(int)kind][index] = 0;
    }

    /// <summary>Resets stance and queue of a slot (the actor is gone).</summary>
    public void ResetOrders(ActorKind kind, int index)
    {
        if ((int)kind >= 2) return;
        _stance[(int)kind][index] = 0;
        ClearWaypoints(kind, index);
    }

    private bool HasOrders(int k, int i)
    {
        if (k >= 2) return false;
        if (_stance[k][i] != 0 || _wpCount[k][i] != 0 || _wpAttack[k][i] != 0) return true;
        int cap = _stance[k].Length;
        for (int w = 0; w < MaxWaypoints; w++) if (_wpCell[k][w * cap + i] != default) return true;
        return false;
    }

    public string Name => "combat";

    /// <summary>Entry for the slot; kind must be Vehicle, Infantry or Building.</summary>
    public ref CombatEntry At(ActorKind kind, int index) => ref _entries[(int)kind][index];

    public void Write(BinaryWriter w)
    {
        for (int k = 0; k < _entries.Length; k++)
        {
            CombatEntry[] arr = _entries[k];
            w.Write(arr.Length);
            for (int i = 1; i < arr.Length; i++)
            {
                ref CombatEntry e = ref arr[i];
                bool orders = HasOrders(k, i);
                if (e.IsDefault && !orders) { w.Write((byte)0); continue; }
                w.Write(orders ? (byte)2 : (byte)1);
                w.Write(e.ReloadTicks); w.Write(e.TurretFacing); w.Write(e.TurretInit);
                w.Write((byte)e.AttackTarget.Kind); w.Write(e.AttackTarget.Id.Index); w.Write(e.AttackTarget.Id.Generation);
                w.Write(e.GuardPoint.X); w.Write(e.GuardPoint.Y); w.Write(e.GuardPending);
                w.Write(e.ChaseCell.X); w.Write(e.ChaseCell.Y);
                w.Write(e.RetargetTick); w.Write(e.ScanTick); w.Write(e.Generation);
                if (!orders) continue;
                w.Write(_stance[k][i]); w.Write(_wpCount[k][i]); w.Write(_wpAttack[k][i]);
                int cap = _stance[k].Length;
                for (int c = 0; c < MaxWaypoints; c++) { w.Write(_wpCell[k][c * cap + i].X); w.Write(_wpCell[k][c * cap + i].Y); }
            }
        }
    }

    public void Read(BinaryReader r)
    {
        for (int k = 0; k < _entries.Length; k++)
        {
            CombatEntry[] arr = _entries[k];
            if (r.ReadInt32() != arr.Length) throw new InvalidDataException("combat capacity mismatch.");
            arr[0] = default;
            for (int i = 1; i < arr.Length; i++)
            {
                ResetOrders((ActorKind)k, i);
                byte flag = r.ReadByte();
                if (flag == 0) { arr[i] = default; continue; }
                if (flag > 2 || (flag == 2 && k >= 2)) throw new InvalidDataException("combat entry flag out of range.");
                var e = new CombatEntry
                {
                    ReloadTicks = r.ReadInt32(), TurretFacing = r.ReadByte(), TurretInit = r.ReadBoolean(),
                };
                var kind = (ActorKind)r.ReadByte();
                e.AttackTarget = new ActorRef(kind, new EntityId(r.ReadUInt16(), r.ReadUInt16()));
                e.GuardPoint = new CellCoord(r.ReadInt32(), r.ReadInt32());
                e.GuardPending = r.ReadBoolean();
                e.ChaseCell = new CellCoord(r.ReadInt32(), r.ReadInt32());
                e.RetargetTick = r.ReadInt32(); e.ScanTick = r.ReadInt32(); e.Generation = r.ReadUInt16();
                arr[i] = e;
                if (flag != 2) continue;
                byte stance = r.ReadByte();
                if (stance > (byte)UnitStance.HoldFire) throw new InvalidDataException("combat stance out of range.");
                byte count = r.ReadByte();
                if (count > MaxWaypoints) throw new InvalidDataException("combat waypoint count out of range.");
                _stance[k][i] = stance; _wpCount[k][i] = count; _wpAttack[k][i] = r.ReadByte();
                int cap = _stance[k].Length;
                for (int c = 0; c < MaxWaypoints; c++) _wpCell[k][c * cap + i] = new CellCoord(r.ReadInt32(), r.ReadInt32());
            }
        }
    }
}
