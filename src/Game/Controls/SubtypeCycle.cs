// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// S47 U-13 (cnc#9): steps through the unit types of a mixed selection. Each press keeps only the units of the next type (in order of first
/// appearance); one press after the last type gives the whole group back. A selection that is not the result of the last press starts a new
/// round. Pure; the game screen supplies the type of a unit.
/// </summary>
public sealed class SubtypeCycle
{
    private List<UnitKey> _base = new();
    private List<UnitKey> _last = new();
    private int _step = -1;

    /// <summary>
    /// The selection after one press, or null when <paramref name="selected"/> has fewer than two types (nothing to cycle).
    /// <paramref name="typeOf"/> gives a unit's kind and type, or null for a unit that is not known (it joins no type).
    /// </summary>
    public IReadOnlyList<UnitKey>? Next(IReadOnlyList<UnitKey> selected, Func<UnitKey, (UnitKind Kind, int Type)?> typeOf)
    {
        if (!SameSet(selected, _last))
        {
            _base = new List<UnitKey>(selected);
            _step = -1;
        }

        var types = new List<(UnitKind Kind, int Type)>();
        foreach (UnitKey key in _base)
            if (typeOf(key) is { } t && !types.Contains(t)) types.Add(t);
        if (types.Count < 2) return null;

        _step = (_step + 1) % (types.Count + 1);
        var result = new List<UnitKey>();
        foreach (UnitKey key in _base)
        {
            if (_step == types.Count || (typeOf(key) is { } t && t == types[_step])) result.Add(key);
        }

        _last = new List<UnitKey>(result);
        return result;
    }

    private static bool SameSet(IReadOnlyList<UnitKey> a, IReadOnlyList<UnitKey> b)
    {
        if (a.Count != b.Count || a.Count == 0) return false;
        foreach (UnitKey k in a) if (!b.Contains(k)) return false;
        return true;
    }
}
