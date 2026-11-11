// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;

namespace Rts.Game.Controls;

/// <summary>
/// The nine control groups (Ctrl+1..9 sets, 1..9 recalls). UI state only: the simulation never sees it, and it is not saved. A unit
/// belongs to at most one group (setting or adding to a group takes the units out of the others, as in classic RTS games), so the
/// number drawn on a unit is unambiguous. Dead units are dropped by <see cref="Prune"/>.
/// </summary>
public sealed class ControlGroups
{
    /// <summary>Number of groups (1..9).</summary>
    public const int Count = 9;

    private readonly List<UnitKey>[] _groups = Enumerable.Range(0, Count).Select(_ => new List<UnitKey>()).ToArray();

    /// <summary>The members of group <paramref name="number"/> (1..9), empty for an out-of-range number.</summary>
    public IReadOnlyList<UnitKey> Members(int number) => InRange(number) ? _groups[number - 1] : Array.Empty<UnitKey>();

    /// <summary>Makes group <paramref name="number"/> exactly the given units (Ctrl+number). An empty set empties the group.</summary>
    public void Set(int number, IEnumerable<UnitKey> units)
    {
        if (!InRange(number)) return;
        List<UnitKey> copy = units.Distinct().ToList();
        foreach (List<UnitKey> g in _groups) g.RemoveAll(copy.Contains);
        _groups[number - 1].Clear();
        _groups[number - 1].AddRange(copy);
    }

    /// <summary>
    /// Ctrl+number (S18 GAMEUI-07): like <see cref="Set"/> but an empty selection leaves the group alone, so a stray Ctrl+1 with nothing
    /// (or only a building) selected does not wipe a hotkeyed army. Returns whether the group was set.
    /// </summary>
    public bool TrySet(int number, IReadOnlyCollection<UnitKey> units)
    {
        if (units.Count == 0 || !InRange(number)) return false;
        Set(number, units);
        return true;
    }

    /// <summary>Adds the given units to group <paramref name="number"/> (Shift+number), taking them out of any other group.</summary>
    public void Add(int number, IEnumerable<UnitKey> units)
    {
        if (!InRange(number)) return;
        List<UnitKey> copy = units.Distinct().ToList();
        foreach (List<UnitKey> g in _groups) g.RemoveAll(copy.Contains);
        _groups[number - 1].AddRange(copy);
    }

    /// <summary>The group number (1..9) a unit belongs to, or 0 when it is in none.</summary>
    public int GroupOf(UnitKey key)
    {
        for (int i = 0; i < Count; i++)
        {
            if (_groups[i].Contains(key)) return i + 1;
        }

        return 0;
    }

    /// <summary>Drops every member for which <paramref name="isAlive"/> is false.</summary>
    public void Prune(Func<UnitKey, bool> isAlive)
    {
        foreach (List<UnitKey> g in _groups) g.RemoveAll(k => !isAlive(k));
    }

    /// <summary>Empties every group (a new game).</summary>
    public void Clear()
    {
        foreach (List<UnitKey> g in _groups) g.Clear();
    }

    private static bool InRange(int number) => number is >= 1 and <= Count;
}
