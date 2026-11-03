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
    }

    public void Remove(EntityId id)
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
            ReadExtra(r, i);
        }
    }
}
