// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;

namespace Rts.Sim.World;

/// <summary>
/// Shared struct-of-arrays storage for actors that run missions (vehicles, infantry), indexed by slot.
/// Mission/Status/WakeTick describe the running mission; NextMission/NextCell/NextTarget the queued one
/// (NextMission == None means nothing queued), promoted by Simulation at the start of RunMissions.
/// </summary>
public abstract class MobileStore : ISimStatePart
{
    public readonly SlotArray Slots;
    public readonly byte[] Player;
    public readonly int[] TypeIndex;
    public readonly LeptonCoord[] Position;
    public readonly byte[] Facing;
    public readonly int[] Health;
    public readonly MissionKind[] Mission;
    public readonly MissionStatus[] Status;
    public readonly int[] WakeTick;
    public readonly LeptonCoord[] Destination;
    public readonly EntityId[] Target;
    public readonly MissionKind[] NextMission;
    public readonly CellCoord[] NextCell;
    public readonly EntityId[] NextTarget;

    // ---- movement state (M1-08). All persisted and hashed. ----
    /// <summary>True while the actor is moving into a cell it has claimed (see <see cref="ReservedCell"/>). Vehicles hold the
    /// reserved cell in the map from the moment they start moving into it; infantry hold a sub-slot of it.</summary>
    public readonly bool[] NextCellReserved;
    /// <summary>The claimed cell while <see cref="NextCellReserved"/> is set.</summary>
    public readonly CellCoord[] ReservedCell;
    /// <summary>Infantry only: the claimed sub-slot in <see cref="ReservedCell"/>.</summary>
    public readonly byte[] ReservedSlot;
    /// <summary>True while <see cref="PathGoal"/> and the path fields describe a route being followed.</summary>
    public readonly bool[] PathActive;
    public readonly CellCoord[] PathGoal;
    /// <summary>PathService request id; 0 = none, -1 = a Move command waits for its (group) request to be created in RunMovement,
    /// -2 = the path was preset (scatter) and must survive mission start.</summary>
    public readonly int[] PathId;
    /// <summary>Index inside a group request.</summary>
    public readonly int[] PathIndex;
    public readonly bool[] PathIsGroup;
    /// <summary>Remaining route (cells after the current one, last = end); null when none.</summary>
    public readonly CellCoord[]?[] PathCells;
    public readonly int[] PathPos;
    /// <summary>MapState.Version when the route was last validated.</summary>
    public readonly int[] PathVersion;
    public readonly int[] BlockedTicks;
    public readonly int[] StuckCount;

    protected MobileStore(int capacity)
    {
        Slots = new SlotArray(capacity);
        Player = new byte[capacity];
        TypeIndex = new int[capacity];
        Position = new LeptonCoord[capacity];
        Facing = new byte[capacity];
        Health = new int[capacity];
        Mission = new MissionKind[capacity];
        Status = new MissionStatus[capacity];
        WakeTick = new int[capacity];
        Destination = new LeptonCoord[capacity];
        Target = new EntityId[capacity];
        NextMission = new MissionKind[capacity];
        NextCell = new CellCoord[capacity];
        NextTarget = new EntityId[capacity];
        NextCellReserved = new bool[capacity];
        ReservedCell = new CellCoord[capacity];
        ReservedSlot = new byte[capacity];
        PathActive = new bool[capacity];
        PathGoal = new CellCoord[capacity];
        PathId = new int[capacity];
        PathIndex = new int[capacity];
        PathIsGroup = new bool[capacity];
        PathCells = new CellCoord[]?[capacity];
        PathPos = new int[capacity];
        PathVersion = new int[capacity];
        BlockedTicks = new int[capacity];
        StuckCount = new int[capacity];
    }

    public abstract string Name { get; }

    public int Capacity => Slots.Capacity;

    /// <summary>Reset every common field of a slot.</summary>
    protected void Reset(int i, byte player, int typeIndex, LeptonCoord pos, int health)
    {
        Player[i] = player; TypeIndex[i] = typeIndex; Position[i] = pos; Facing[i] = 0; Health[i] = health;
        Mission[i] = MissionKind.None; Status[i] = MissionStatus.Init; WakeTick[i] = 0;
        Destination[i] = pos; Target[i] = default;
        NextMission[i] = MissionKind.None; NextCell[i] = default; NextTarget[i] = default;
        NextCellReserved[i] = false; ReservedCell[i] = default; ReservedSlot[i] = 0;
        PathActive[i] = false; PathGoal[i] = default; PathId[i] = 0; PathIndex[i] = 0; PathIsGroup[i] = false;
        PathCells[i] = null; PathPos[i] = 0; PathVersion[i] = 0; BlockedTicks[i] = 0; StuckCount[i] = 0;
    }

    internal void Remove(EntityId id)
    {
        if (!Slots.Free(id)) return;
        int i = id.Index;
        Reset(i, 0, 0, default, 0);
        ResetExtra(i);
    }

    protected virtual void ResetExtra(int i) { }
    protected virtual void WriteExtra(BinaryWriter w, int i) { }
    protected virtual void ReadExtra(BinaryReader r, int i) { }

    /// <summary>Canonical serialisation: per slot generation + alive flag, then fields of alive slots.</summary>
    public void Write(BinaryWriter w)
    {
        w.Write(Capacity);
        for (int i = 1; i < Capacity; i++)
        {
            bool alive = Slots.IsIndexAlive(i);
            w.Write(Slots.GenerationOf(i));
            w.Write(alive);
            if (!alive) continue;
            w.Write(Player[i]); w.Write(TypeIndex[i]);
            w.Write(Position[i].X); w.Write(Position[i].Y);
            w.Write(Facing[i]); w.Write(Health[i]);
            w.Write((byte)Mission[i]); w.Write((byte)Status[i]); w.Write(WakeTick[i]);
            w.Write(Destination[i].X); w.Write(Destination[i].Y);
            w.Write(Target[i].Index); w.Write(Target[i].Generation);
            w.Write((byte)NextMission[i]);
            w.Write(NextCell[i].X); w.Write(NextCell[i].Y);
            w.Write(NextTarget[i].Index); w.Write(NextTarget[i].Generation);
            w.Write(NextCellReserved[i]); w.Write(ReservedCell[i].X); w.Write(ReservedCell[i].Y); w.Write(ReservedSlot[i]);
            w.Write(PathActive[i]); w.Write(PathGoal[i].X); w.Write(PathGoal[i].Y);
            w.Write(PathId[i]); w.Write(PathIndex[i]); w.Write(PathIsGroup[i]);
            CellCoord[]? cells = PathCells[i];
            w.Write(cells?.Length ?? -1);
            if (cells != null) for (int k = 0; k < cells.Length; k++) { w.Write(cells[k].X); w.Write(cells[k].Y); }
            w.Write(PathPos[i]); w.Write(PathVersion[i]); w.Write(BlockedTicks[i]); w.Write(StuckCount[i]);
            WriteExtra(w, i);
        }
    }

    public void Read(BinaryReader r)
    {
        int cap = r.ReadInt32();
        if (cap != Capacity) throw new InvalidDataException($"{Name} capacity mismatch.");
        for (int i = 1; i < Capacity; i++)
        {
            ushort gen = r.ReadUInt16();
            bool alive = r.ReadBoolean();
            Slots.Restore(i, gen, alive);
            if (!alive) { Reset(i, 0, 0, default, 0); ResetExtra(i); continue; }
            Player[i] = r.ReadByte(); TypeIndex[i] = r.ReadInt32();
            Position[i] = new LeptonCoord(r.ReadInt32(), r.ReadInt32());
            Facing[i] = r.ReadByte(); Health[i] = r.ReadInt32();
            Mission[i] = (MissionKind)r.ReadByte(); Status[i] = (MissionStatus)r.ReadByte(); WakeTick[i] = r.ReadInt32();
            Destination[i] = new LeptonCoord(r.ReadInt32(), r.ReadInt32());
            Target[i] = new EntityId(r.ReadUInt16(), r.ReadUInt16());
            NextMission[i] = (MissionKind)r.ReadByte();
            NextCell[i] = new CellCoord(r.ReadInt32(), r.ReadInt32());
            NextTarget[i] = new EntityId(r.ReadUInt16(), r.ReadUInt16());
            NextCellReserved[i] = r.ReadBoolean(); ReservedCell[i] = new CellCoord(r.ReadInt32(), r.ReadInt32()); ReservedSlot[i] = r.ReadByte();
            PathActive[i] = r.ReadBoolean(); PathGoal[i] = new CellCoord(r.ReadInt32(), r.ReadInt32());
            PathId[i] = r.ReadInt32(); PathIndex[i] = r.ReadInt32(); PathIsGroup[i] = r.ReadBoolean();
            int len = r.ReadInt32();
            if (len < 0) PathCells[i] = null;
            else
            {
                if (len > 1_000_000) throw new InvalidDataException("Path too long.");
                var cells = new CellCoord[len];
                for (int k = 0; k < len; k++) cells[k] = new CellCoord(r.ReadInt32(), r.ReadInt32());
                PathCells[i] = cells;
            }
            PathPos[i] = r.ReadInt32(); PathVersion[i] = r.ReadInt32(); BlockedTicks[i] = r.ReadInt32(); StuckCount[i] = r.ReadInt32();
            ReadExtra(r, i);
        }
    }
}
