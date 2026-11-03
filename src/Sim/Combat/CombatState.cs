// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Combat;

/// <summary>
/// State part "combat": a <see cref="CombatEntry"/> per slot for vehicles, infantry and buildings (index = slot).
/// Serialisation: for each kind (vehicle, infantry, building) the capacity, then per slot 1..cap-1 a flag byte
/// (0 = default entry) followed, if set, by the entry fields in declaration order.
/// </summary>
public sealed class CombatState : ISimStatePart
{
    private readonly CombatEntry[][] _entries;

    public CombatState(int vehicleCapacity = UnitStore.DefaultCapacity, int infantryCapacity = InfantryStore.DefaultCapacity,
        int buildingCapacity = BuildingStore.DefaultCapacity)
    {
        _entries = new[]
        {
            new CombatEntry[vehicleCapacity], new CombatEntry[infantryCapacity], new CombatEntry[buildingCapacity],
        };
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
                if (e.IsDefault) { w.Write((byte)0); continue; }
                w.Write((byte)1);
                w.Write(e.ReloadTicks); w.Write(e.TurretFacing); w.Write(e.TurretInit);
                w.Write((byte)e.AttackTarget.Kind); w.Write(e.AttackTarget.Id.Index); w.Write(e.AttackTarget.Id.Generation);
                w.Write(e.GuardPoint.X); w.Write(e.GuardPoint.Y); w.Write(e.GuardPending);
                w.Write(e.ChaseCell.X); w.Write(e.ChaseCell.Y);
                w.Write(e.RetargetTick); w.Write(e.ScanTick); w.Write(e.Generation);
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
                if (r.ReadByte() == 0) { arr[i] = default; continue; }
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
            }
        }
    }
}
