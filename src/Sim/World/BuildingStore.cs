using Rts.Sim.Core;
using Rts.Sim.Missions;

namespace Rts.Sim.World;

/// <summary>
/// Struct-of-arrays storage for buildings, indexed by slot. Cell is the top-left of the footprint (the
/// footprint size comes from the building type). Mission/Status/WakeTick/Target mirror <see cref="MobileStore"/>
/// so turrets can fight in a later wave; no handlers run for buildings yet.
/// </summary>
public sealed class BuildingStore : ISimStatePart
{
    public const int DefaultCapacity = 512;

    public readonly SlotArray Slots;
    public readonly byte[] Player;
    public readonly int[] TypeIndex;
    public readonly CellCoord[] Cell;
    public readonly int[] Health;
    public readonly MissionKind[] Mission;
    public readonly MissionStatus[] Status;
    public readonly int[] WakeTick;
    public readonly EntityId[] Target;

    public BuildingStore(int capacity = DefaultCapacity)
    {
        Slots = new SlotArray(capacity);
        Player = new byte[capacity];
        TypeIndex = new int[capacity];
        Cell = new CellCoord[capacity];
        Health = new int[capacity];
        Mission = new MissionKind[capacity];
        Status = new MissionStatus[capacity];
        WakeTick = new int[capacity];
        Target = new EntityId[capacity];
    }

    public string Name => "buildings";

    public int Capacity => Slots.Capacity;

    /// <summary>Allocate a slot and reset every field of it. Returns <see cref="EntityId.None"/> when the store is full.</summary>
    internal EntityId Add(byte player, int typeIndex, CellCoord topLeft, int health)
    {
        if (!Slots.TryAllocate(out EntityId id)) return EntityId.None;
        Reset(id.Index, player, typeIndex, topLeft, health);
        return id;
    }

    /// <summary>Free the slot and clear its fields. No-op for a stale id.</summary>
    internal void Remove(EntityId id)
    {
        if (!Slots.Free(id)) return;
        Reset(id.Index, 0, 0, default, 0);
    }

    private void Reset(int i, byte player, int typeIndex, CellCoord cell, int health)
    {
        Player[i] = player; TypeIndex[i] = typeIndex; Cell[i] = cell; Health[i] = health;
        Mission[i] = MissionKind.None; Status[i] = MissionStatus.Init; WakeTick[i] = 0; Target[i] = default;
    }

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
            w.Write(Cell[i].X); w.Write(Cell[i].Y);
            w.Write(Health[i]);
            w.Write((byte)Mission[i]); w.Write((byte)Status[i]); w.Write(WakeTick[i]);
            w.Write(Target[i].Index); w.Write(Target[i].Generation);
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
            if (!alive) { Reset(i, 0, 0, default, 0); continue; }
            Player[i] = r.ReadByte(); TypeIndex[i] = r.ReadInt32();
            Cell[i] = new CellCoord(r.ReadInt32(), r.ReadInt32());
            Health[i] = r.ReadInt32();
            Mission[i] = (MissionKind)r.ReadByte(); Status[i] = (MissionStatus)r.ReadByte(); WakeTick[i] = r.ReadInt32();
            Target[i] = new EntityId(r.ReadUInt16(), r.ReadUInt16());
        }
    }
}
