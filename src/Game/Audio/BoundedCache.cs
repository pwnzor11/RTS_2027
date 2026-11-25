// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// A least-recently-used cache with a fixed capacity (V-06: decoded voice clips). <see cref="GetOrAdd"/> returns the cached value or
/// creates it; the value may be null, so a clip that failed to decode is remembered and not decoded again on every call. When a new
/// entry makes the cache too big, the entry that was used longest ago is dropped. Pure, so it is unit-tested without an audio device.
/// </summary>
/// <typeparam name="TKey">The key.</typeparam>
/// <typeparam name="TValue">The cached value.</typeparam>
public sealed class BoundedCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _map;
    private readonly LinkedList<KeyValuePair<TKey, TValue>> _recent = new();   // most recently used first

    /// <summary>Creates a cache that holds at most <paramref name="capacity"/> entries (at least 1).</summary>
    public BoundedCache(int capacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "a cache holds at least one entry");
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(capacity);
    }

    /// <summary>The most entries the cache holds.</summary>
    public int Capacity => _capacity;

    /// <summary>The entries held now.</summary>
    public int Count => _map.Count;

    /// <summary>True when <paramref name="key"/> is held.</summary>
    public bool Contains(TKey key) => _map.ContainsKey(key);

    /// <summary>The value for <paramref name="key"/>; when it is not held, <paramref name="create"/> makes it (called once) and it is held from then on.</summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> create)
    {
        if (_map.TryGetValue(key, out LinkedListNode<KeyValuePair<TKey, TValue>>? node))
        {
            _recent.Remove(node);
            _recent.AddFirst(node);
            return node.Value.Value;
        }

        TValue value = create(key);
        LinkedListNode<KeyValuePair<TKey, TValue>> added = _recent.AddFirst(new KeyValuePair<TKey, TValue>(key, value));
        _map[key] = added;
        if (_map.Count > _capacity)
        {
            LinkedListNode<KeyValuePair<TKey, TValue>>? oldest = _recent.Last;
            if (oldest is not null)
            {
                _recent.RemoveLast();
                _map.Remove(oldest.Value.Key);
            }
        }

        return value;
    }
}
