using Rts.Sim.Core;

namespace Rts.Sim.World;

/// <summary>Struct-of-arrays storage holding all unit components, indexed by slot.</summary>
public sealed class UnitStore : ISimStatePart
{
    public const int DefaultCapacity = 2048;

    public readonly SlotArray Slots;
    public readonly byte[] Player;
    public readonly int[] TypeIndex;
    public readonly LeptonCoord[] Position;
    public readonly byte[] Facing;
    public readonly int[] Health;
    public readonly UnitMission[] Mission;
    public readonly LeptonCoord[] Destination;

    public UnitStore(int capacity = DefaultCapacity)
    {
        Slots = new SlotArray(capacity);
        Player = new byte[capacity];
        TypeIndex = new int[capacity];
        Position = new LeptonCoord[capacity];
        Facing = new byte[capacity];
        Health = new int[capacity];
        Mission = new UnitMission[capacity];
        Destination = new LeptonCoord[capacity];
    }

    public string Name => "units";

    public int Capacity => Slots.Capacity;

    /// <summary>Allocate a slot and reset every field of it.</summary>
    public EntityId Add(byte player, int typeIndex, LeptonCoord pos, int health)
    {
        EntityId id = Slots.Allocate();
        int i = id.Index;
        Player[i] = player; TypeIndex[i] = typeIndex; Position[i] = pos; Facing[i] = 0;
        Health[i] = health; Mission[i] = UnitMission.Idle; Destination[i] = pos;
        return id;
    }

    public void Remove(EntityId id)
    {
        if (!Slots.Free(id)) return;
        int i = id.Index;
        Player[i] = 0; TypeIndex[i] = 0; Position[i] = default; Facing[i] = 0;
        Health[i] = 0; Mission[i] = UnitMission.Idle; Destination[i] = default;
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
            w.Write(Position[i].X); w.Write(Position[i].Y);
            w.Write(Facing[i]); w.Write(Health[i]); w.Write((byte)Mission[i]);
            w.Write(Destination[i].X); w.Write(Destination[i].Y);
        }
    }

    public void Read(BinaryReader r)
    {
        int cap = r.ReadInt32();
        if (cap != Capacity) throw new InvalidDataException("Unit capacity mismatch.");
        for (int i = 1; i < Capacity; i++)
        {
            ushort gen = r.ReadUInt16();
            bool alive = r.ReadBoolean();
            Slots.Restore(i, gen, alive);
            if (!alive) continue;
            Player[i] = r.ReadByte(); TypeIndex[i] = r.ReadInt32();
            Position[i] = new LeptonCoord(r.ReadInt32(), r.ReadInt32());
            Facing[i] = r.ReadByte(); Health[i] = r.ReadInt32(); Mission[i] = (UnitMission)r.ReadByte();
            Destination[i] = new LeptonCoord(r.ReadInt32(), r.ReadInt32());
        }
    }
}
