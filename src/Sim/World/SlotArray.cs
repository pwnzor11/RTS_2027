// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Numerics;
using Rts.Sim.Core;

namespace Rts.Sim.World;

/// <summary>
/// Fixed-capacity slot allocator for one entity kind. Index 0 is reserved. Allocation always returns the
/// lowest free index (bitset scan), so it is deterministic. Generation is bumped on every allocation.
/// </summary>
public sealed class SlotArray
{
    private readonly ulong[] _used;
    private readonly ushort[] _generation;
    private int _count;

    public SlotArray(int capacity)
    {
        if (capacity < 2 || capacity > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _used = new ulong[(capacity + 63) / 64];
        _generation = new ushort[capacity];
        _used[0] |= 1UL; // index 0 reserved
    }

    public int Capacity { get; }

    /// <summary>Number of alive entities.</summary>
    public int Count => _count;

    /// <summary>Allocate the lowest free index; throws when full. Simulation code uses <see cref="TryAllocate"/>.</summary>
    internal EntityId Allocate()
    {
        if (!TryAllocate(out EntityId id)) throw new InvalidOperationException("SlotArray is full.");
        return id;
    }

    /// <summary>Allocate the lowest free index. Returns false (and <see cref="EntityId.None"/>) when full.</summary>
    internal bool TryAllocate(out EntityId id)
    {
        for (int w = 0; w < _used.Length; w++)
        {
            ulong free = ~_used[w];
            if (free == 0) continue;
            int idx = w * 64 + BitOperations.TrailingZeroCount(free);
            if (idx >= Capacity) break;
            _used[w] |= 1UL << (idx & 63);
            ushort g = unchecked((ushort)(_generation[idx] + 1));
            if (g == 0) g = 1;
            _generation[idx] = g;
            _count++;
            id = new EntityId((ushort)idx, g);
            return true;
        }
        id = EntityId.None;
        return false;
    }

    /// <summary>Free a live entity. Returns false if the id is stale.</summary>
    internal bool Free(EntityId id)
    {
        if (!IsAlive(id)) return false;
        _used[id.Index >> 6] &= ~(1UL << (id.Index & 63));
        _count--;
        return true;
    }

    public bool IsAlive(EntityId id) =>
        id.Index != 0 && id.Index < Capacity && IsIndexAlive(id.Index) && _generation[id.Index] == id.Generation;

    public bool IsIndexAlive(int index) => index > 0 && index < Capacity && (_used[index >> 6] & (1UL << (index & 63))) != 0;

    public ushort GenerationOf(int index) => _generation[index];

    public EntityId IdOf(int index) => new((ushort)index, _generation[index]);

    /// <summary>Alive indexes in ascending order.</summary>
    public IEnumerable<int> AliveIndexes()
    {
        for (int i = 1; i < Capacity; i++)
            if (IsIndexAlive(i)) yield return i;
    }

    /// <summary>Restore one slot's state when loading a save.</summary>
    internal void Restore(int index, ushort generation, bool alive)
    {
        if (index <= 0 || index >= Capacity) throw new ArgumentOutOfRangeException(nameof(index));
        bool was = IsIndexAlive(index);
        _generation[index] = generation;
        ulong bit = 1UL << (index & 63);
        if (alive && !was) { _used[index >> 6] |= bit; _count++; }
        else if (!alive && was) { _used[index >> 6] &= ~bit; _count--; }
    }
}
