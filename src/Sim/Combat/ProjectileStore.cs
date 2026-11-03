using Rts.Sim.Core;
using Rts.Sim.World;

namespace Rts.Sim.Combat;

/// <summary>
/// State part "projectiles": fixed 1024-slot store of shells in flight. A projectile flies straight at Speed
/// leptons/tick to TargetPos (the target's position at launch, no homing) and detonates there. Target is only
/// informational and is cleared when the target dies. Source = weapon index, Warhead = warhead index.
/// Serialisation: capacity, then per slot generation + alive flag and, if alive, the fields in declaration order.
/// </summary>
public sealed class ProjectileStore : ISimStatePart
{
    public const int DefaultCapacity = 1024;

    public readonly SlotArray Slots;
    public readonly LeptonCoord[] Position;
    public readonly ActorRef[] Target;
    public readonly LeptonCoord[] TargetPos;
    public readonly int[] Speed;
    public readonly int[] Damage;
    public readonly int[] Warhead;
    public readonly byte[] Owner;
    public readonly int[] Source;

    public ProjectileStore(int capacity = DefaultCapacity)
    {
        Slots = new SlotArray(capacity);
        Position = new LeptonCoord[capacity];
        Target = new ActorRef[capacity];
        TargetPos = new LeptonCoord[capacity];
        Speed = new int[capacity];
        Damage = new int[capacity];
        Warhead = new int[capacity];
        Owner = new byte[capacity];
        Source = new int[capacity];
    }

    public string Name => "projectiles";

    public int Capacity => Slots.Capacity;

    /// <summary>Impact cell of the projectile in slot <paramref name="i"/>.</summary>
    public CellCoord TargetCell(int i) => TargetPos[i].ToCell();

    /// <summary>True if another projectile can be spawned.</summary>
    public bool HasFree => Slots.Count < Slots.Capacity - 1;

    /// <summary>Allocate and fill a projectile slot (caller checks <see cref="HasFree"/>).</summary>
    public EntityId Spawn(LeptonCoord from, ActorRef target, LeptonCoord targetPos, int speed, int damage, int warhead,
        byte owner, int weapon)
    {
        EntityId id = Slots.Allocate();
        int i = id.Index;
        Position[i] = from; Target[i] = target; TargetPos[i] = targetPos; Speed[i] = speed; Damage[i] = damage;
        Warhead[i] = warhead; Owner[i] = owner; Source[i] = weapon;
        return id;
    }

    /// <summary>Free the slot and clear its fields. No-op for a stale id.</summary>
    public void Free(EntityId id)
    {
        if (!Slots.Free(id)) return;
        Clear(id.Index);
    }

    private void Clear(int i)
    {
        Position[i] = default; Target[i] = default; TargetPos[i] = default; Speed[i] = 0; Damage[i] = 0;
        Warhead[i] = 0; Owner[i] = 0; Source[i] = 0;
    }

    public void Write(BinaryWriter w)
    {
        w.Write(Capacity);
        for (int i = 1; i < Capacity; i++)
        {
            bool alive = Slots.IsIndexAlive(i);
            w.Write(Slots.GenerationOf(i));
            w.Write(alive);
            if (!alive) continue;
            w.Write(Position[i].X); w.Write(Position[i].Y);
            w.Write((byte)Target[i].Kind); w.Write(Target[i].Id.Index); w.Write(Target[i].Id.Generation);
            w.Write(TargetPos[i].X); w.Write(TargetPos[i].Y);
            w.Write(Speed[i]); w.Write(Damage[i]); w.Write(Warhead[i]); w.Write(Owner[i]); w.Write(Source[i]);
        }
    }

    public void Read(BinaryReader r)
    {
        if (r.ReadInt32() != Capacity) throw new InvalidDataException("projectiles capacity mismatch.");
        for (int i = 1; i < Capacity; i++)
        {
            ushort gen = r.ReadUInt16();
            bool alive = r.ReadBoolean();
            Slots.Restore(i, gen, alive);
            if (!alive) { Clear(i); continue; }
            Position[i] = new LeptonCoord(r.ReadInt32(), r.ReadInt32());
            var kind = (ActorKind)r.ReadByte();
            Target[i] = new ActorRef(kind, new EntityId(r.ReadUInt16(), r.ReadUInt16()));
            TargetPos[i] = new LeptonCoord(r.ReadInt32(), r.ReadInt32());
            Speed[i] = r.ReadInt32(); Damage[i] = r.ReadInt32(); Warhead[i] = r.ReadInt32();
            Owner[i] = r.ReadByte(); Source[i] = r.ReadInt32();
        }
    }
}
