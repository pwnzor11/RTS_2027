// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.World;

/// <summary>Struct-of-arrays storage holding all vehicle components, indexed by slot.</summary>
public sealed class UnitStore : MobileStore
{
    public const int DefaultCapacity = 2048;

    public UnitStore(int capacity = DefaultCapacity) : base(capacity) { }

    public override string Name => "units";

    /// <summary>Allocate a slot and reset every field of it. Returns <see cref="EntityId.None"/> when the store is full.</summary>
    internal EntityId Add(byte player, int typeIndex, LeptonCoord pos, int health)
    {
        if (!Slots.TryAllocate(out EntityId id)) return EntityId.None;
        Reset(id.Index, player, typeIndex, pos, health);
        return id;
    }
}
