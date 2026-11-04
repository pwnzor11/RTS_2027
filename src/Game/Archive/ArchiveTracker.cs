// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Archive;

/// <summary>
/// Feeds <see cref="ArchiveProgress"/> from the game. Call <see cref="Observe"/> with each new snapshot and
/// <see cref="ObserveResearch"/> with the player's upgrade mask (<c>PlayerHud.Upgrades</c>). It only reads what the
/// viewer's snapshot already contains, which the sim filters by fog (enemy units outside vision are omitted; enemy
/// buildings are listed only once seen), so an encounter is never recorded from information the player does not have.
/// An omniscient snapshot (Viewer &lt; 0, the debug view) records nothing. The progress file is saved when it changes.
/// </summary>
public sealed class ArchiveTracker
{
    private readonly ArchiveProgress _progress;
    private readonly string? _path;
    private readonly HashSet<(int Group, int Index)> _seen = new();
    private bool _pending;

    /// <summary>Creates a tracker over a progress record. <paramref name="path"/> null uses <see cref="ArchiveProgress.DefaultPath"/>; pass a path in tests.</summary>
    public ArchiveTracker(ArchiveProgress progress, string? path = null)
    {
        _progress = progress;
        _path = path;
    }

    /// <summary>The progress being updated.</summary>
    public ArchiveProgress Progress => _progress;

    /// <summary>Records every unit and building type the viewer can currently see (or has seen as a ghost). Returns true if anything new was recorded.</summary>
    public bool Observe(SimSnapshot snapshot, SimRules rules)
    {
        if (snapshot.Viewer < 0) return false;
        foreach (UnitView u in snapshot.Units)
        {
            if (u.Underground && u.Player != snapshot.Viewer) continue;
            if (u.Kind == UnitKind.Vehicle) Note(0, u.TypeIndex, rules.Vehicles.Count, i => rules.Vehicles[i].Id);
            else Note(1, u.TypeIndex, rules.Infantry.Count, i => rules.Infantry[i].Id);
        }
        foreach (BuildingView b in snapshot.Buildings) Note(2, b.TypeIndex, rules.Buildings.Count, i => rules.Buildings[i].Id);
        return Flush();
    }

    /// <summary>Records every upgrade whose bit is set in <paramref name="upgradeMask"/>. Returns true if anything new was recorded.</summary>
    public bool ObserveResearch(SimRules rules, ulong upgradeMask)
    {
        for (int i = 0; i < rules.Upgrades.Count && i < 64; i++)
        {
            if ((upgradeMask & (1UL << i)) != 0 && _seen.Add((3, i))) _pending |= _progress.RecordResearch(rules.Upgrades[i].Id);
        }
        return Flush();
    }

    private void Note(int group, int index, int count, Func<int, string> idOf)
    {
        if (index < 0 || index >= count || !_seen.Add((group, index))) return;
        _pending |= _progress.RecordEncounter(idOf(index));
    }

    private bool Flush()
    {
        if (!_pending) return false;
        _pending = false;   // one save attempt per change; a failed write is retried at the next change
        _progress.Save(_path);
        return true;
    }
}
