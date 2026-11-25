// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>
/// The nine control groups (Ctrl+1..9 sets, 1..9 recalls). UI state (the simulation never sees it; the game save file keeps it, D-205, through <see cref="Snapshot"/> and <see cref="Restore"/>). A unit
/// belongs to at most one group (setting or adding to a group takes the units out of the others, as in classic RTS games), so the
/// number drawn on a unit is unambiguous. Dead units are dropped by <see cref="Prune"/>.
/// </summary>
public sealed class ControlGroups
{
    /// <summary>Number of groups (1..9).</summary>
    public const int Count = 9;

    /// <summary>
    /// QOL-17: when true, recalling a group selects every unit of the group's types, map-wide (the membership itself is unchanged). UI
    /// state, not saved; off by default, so a recall selects exactly the members as before.
    /// </summary>
    public bool AutoByType { get; set; }

    private readonly List<EntityId>[] _structures = Enumerable.Range(0, Count).Select(_ => new List<EntityId>()).ToArray();

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
        _structures[number - 1].Clear();   // U-05: setting a group replaces it whole
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

    /// <summary>U-05: the structures of group <paramref name="number"/> (1..9), empty for an out-of-range number. Structures are UI state only (not in the save file).</summary>
    public IReadOnlyList<EntityId> Structures(int number) => InRange(number) ? _structures[number - 1] : Array.Empty<EntityId>();

    /// <summary>
    /// U-05: Ctrl+number with a building selected. The group becomes exactly these structures (its units and old structures leave it), and the
    /// structures leave any other group. An empty set changes nothing. Returns whether the group was set.
    /// </summary>
    public bool TrySetStructures(int number, IReadOnlyCollection<EntityId> structures)
    {
        if (structures.Count == 0 || !InRange(number)) return false;
        List<EntityId> copy = structures.Distinct().ToList();
        foreach (List<EntityId> g in _structures) g.RemoveAll(copy.Contains);
        _structures[number - 1].Clear();
        _structures[number - 1].AddRange(copy);
        _groups[number - 1].Clear();
        return true;
    }

    /// <summary>U-05: Shift+number with a building selected: adds the structures to the group, taking them out of any other group.</summary>
    public void AddStructures(int number, IEnumerable<EntityId> structures)
    {
        if (!InRange(number)) return;
        List<EntityId> copy = structures.Distinct().ToList();
        foreach (List<EntityId> g in _structures) g.RemoveAll(copy.Contains);
        _structures[number - 1].AddRange(copy);
    }

    /// <summary>U-05: the group number (1..9) a structure belongs to, or 0 when it is in none.</summary>
    public int GroupOfStructure(EntityId id)
    {
        for (int i = 0; i < Count; i++)
            if (_structures[i].Contains(id)) return i + 1;
        return 0;
    }

    /// <summary>U-05: drops every structure for which <paramref name="isAlive"/> is false (sold or destroyed).</summary>
    public void PruneStructures(Func<EntityId, bool> isAlive)
    {
        foreach (List<EntityId> g in _structures) g.RemoveAll(id => !isAlive(id));
    }

    /// <summary>
    /// U-05: what a number key does. Ctrl sets the group from the selected units, or (no units selected) from the selected structures; Shift
    /// adds the selection (units, else structures) and keeps the group; a plain press recalls the group's units, or (it has none) its structures.
    /// An empty selection never changes a group. The caller then does the selecting, sets the camera and so on.
    /// </summary>
    public GroupPress Press(int number, bool ctrl, bool shift, IReadOnlyCollection<UnitKey> selectedUnits, IReadOnlyCollection<EntityId> selectedStructures)
    {
        if (!InRange(number)) return GroupPress.None;
        if (ctrl || shift)
        {
            if (selectedUnits.Count > 0)
            {
                if (ctrl) Set(number, selectedUnits); else Add(number, selectedUnits);
                return ctrl ? GroupPress.Set : GroupPress.Added;
            }

            if (selectedStructures.Count == 0) return GroupPress.None;
            if (ctrl) TrySetStructures(number, selectedStructures); else AddStructures(number, selectedStructures);
            return ctrl ? GroupPress.Set : GroupPress.Added;
        }

        if (_groups[number - 1].Count > 0) return GroupPress.RecallUnits;
        return _structures[number - 1].Count > 0 ? GroupPress.RecallStructures : GroupPress.None;
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

    /// <summary>The members of all nine groups (index 0 = group 1), copied, for the save file (D-205).</summary>
    public IReadOnlyList<IReadOnlyList<UnitKey>> Snapshot() => _groups.Select(g => (IReadOnlyList<UnitKey>)g.ToArray()).ToArray();

    /// <summary>
    /// Replaces every group with the saved ones (index 0 = group 1; missing entries leave a group empty). A unit listed twice keeps
    /// its first group, so the one-group-per-unit rule still holds for a hand-edited or damaged save.
    /// </summary>
    public void Restore(IReadOnlyList<IReadOnlyList<UnitKey>> saved)
    {
        Clear();
        var seen = new HashSet<UnitKey>();
        for (int i = 0; i < Count && i < saved.Count; i++)
            foreach (UnitKey key in saved[i])
                if (seen.Add(key)) _groups[i].Add(key);
    }

    /// <summary>Empties every group (a new game).</summary>
    public void Clear()
    {
        foreach (List<UnitKey> g in _groups) g.Clear();
        foreach (List<EntityId> g in _structures) g.Clear();
    }

    private static bool InRange(int number) => number is >= 1 and <= Count;
}

/// <summary>U-05: the outcome of a control-group key press (<see cref="ControlGroups.Press"/>).</summary>
public enum GroupPress
{
    /// <summary>Nothing happened (empty selection or empty group).</summary>
    None,
    /// <summary>Ctrl+number replaced the group with the selection.</summary>
    Set,
    /// <summary>Shift+number added the selection to the group.</summary>
    Added,
    /// <summary>The group's units are to be selected.</summary>
    RecallUnits,
    /// <summary>The group has no units; its structures are to be selected.</summary>
    RecallStructures,
}
