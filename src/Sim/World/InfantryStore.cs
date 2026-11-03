using Rts.Sim.Core;

namespace Rts.Sim.World;

/// <summary>Struct-of-arrays storage for infantry, indexed by slot. SubSlot 0..4 is the position inside the cell.</summary>
public sealed class InfantryStore : MobileStore
{
    public const int DefaultCapacity = 2048;
    public const int SubSlotCount = 5;
    private const int Quarter = SimConstants.LeptonsPerCell / 4; // 64

    public readonly byte[] SubSlot;

    public InfantryStore(int capacity = DefaultCapacity) : base(capacity) => SubSlot = new byte[capacity];

    public override string Name => "infantry";

    /// <summary>Offset in leptons from the cell centre: 0 = centre, 1..4 = NW, NE, SW, SE corners (+-64).</summary>
    public static LeptonCoord SubSlotOffset(int subSlot) => subSlot switch
    {
        0 => new LeptonCoord(0, 0),
        1 => new LeptonCoord(-Quarter, -Quarter),
        2 => new LeptonCoord(Quarter, -Quarter),
        3 => new LeptonCoord(-Quarter, Quarter),
        4 => new LeptonCoord(Quarter, Quarter),
        _ => throw new ArgumentOutOfRangeException(nameof(subSlot)),
    };

    /// <summary>Position in leptons of a sub-slot within a cell.</summary>
    public static LeptonCoord SlotPosition(CellCoord cell, int subSlot)
    {
        LeptonCoord c = LeptonCoord.CenterOf(cell), o = SubSlotOffset(subSlot);
        return new LeptonCoord(c.X + o.X, c.Y + o.Y);
    }

    /// <summary>Allocate a slot and reset every field of it.</summary>
    public EntityId Add(byte player, int typeIndex, CellCoord cell, int subSlot, int health)
    {
        LeptonCoord pos = SlotPosition(cell, subSlot);
        EntityId id = Slots.Allocate();
        Reset(id.Index, player, typeIndex, pos, health);
        SubSlot[id.Index] = (byte)subSlot;
        return id;
    }

    protected override void ResetExtra(int i) => SubSlot[i] = 0;
    protected override void WriteExtra(BinaryWriter w, int i) => w.Write(SubSlot[i]);
    protected override void ReadExtra(BinaryReader r, int i) => SubSlot[i] = r.ReadByte();
}
