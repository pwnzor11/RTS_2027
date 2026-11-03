using Rts.Sim.Core;

namespace Rts.Sim.World;

/// <summary>Struct-of-arrays storage holding all vehicle components, indexed by slot.</summary>
public sealed class UnitStore : MobileStore
{
    public const int DefaultCapacity = 2048;

    public UnitStore(int capacity = DefaultCapacity) : base(capacity) { }

    public override string Name => "units";

    /// <summary>Allocate a slot and reset every field of it.</summary>
    public EntityId Add(byte player, int typeIndex, LeptonCoord pos, int health)
    {
        EntityId id = Slots.Allocate();
        Reset(id.Index, player, typeIndex, pos, health);
        return id;
    }
}
