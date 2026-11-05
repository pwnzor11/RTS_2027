// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11: which army units are taken out of the wave pool and why. Retreated units guard the base (<c>Home</c>); the scout and the
/// raiding party are on special duty (<c>Detached</c>). <see cref="Apply"/> moves them from <see cref="RecruitKnowledge.Army"/> into
/// the matching knowledge lists each decision and forgets units that died. Keys only, never sim state.
/// </summary>
internal sealed class Squads
{
    private readonly HashSet<ulong> _home = new(), _scout = new(), _raiders = new();
    private readonly List<ulong> _dead = new();
    // VET-6: retreated rank-3 units that are being repaired (key -> tick they left); they sit in Detached and rejoin the pool when healed
    private readonly Dictionary<ulong, int> _repair = new();

    /// <summary>Number of raiders alive as of the last <see cref="Apply"/>.</summary>
    public int RaiderCount => _raiders.Count;

    /// <summary>True when a scout is assigned.</summary>
    public bool HasScout => _scout.Count > 0;

    /// <summary>Total units retreated so far (statistics).</summary>
    public int Retreated { get; private set; }

    public bool IsHome(ViewActor a) => _home.Contains(RecruitKnowledge.KeyOf(a));
    public bool IsScout(ViewActor a) => _scout.Contains(RecruitKnowledge.KeyOf(a));
    public bool IsRaider(ViewActor a) => _raiders.Contains(RecruitKnowledge.KeyOf(a));

    /// <summary>Takes a unit out of the wave pool for good: it guards the base (it does not heal, so it never rejoins a wave).</summary>
    public void SendHome(RecruitKnowledge k, ViewActor a)
    {
        ulong key = RecruitKnowledge.KeyOf(a);
        _scout.Remove(key); _raiders.Remove(key);
        if (_home.Add(key)) Retreated++;
        if (!k.Army.Remove(a)) k.Detached.Remove(a);
        k.Home.Add(a);
    }

    /// <summary>VET-6: true when the unit is a retreated rank-3 unit waiting for repair.</summary>
    public bool IsRepairing(ViewActor a) => _repair.ContainsKey(RecruitKnowledge.KeyOf(a));

    /// <summary>VET-6: takes a unit out of the wave pool until <see cref="Release"/>: it is on special duty (Detached) and not home guard.</summary>
    public void SendRepair(RecruitKnowledge k, ViewActor a, int tick)
    {
        ulong key = RecruitKnowledge.KeyOf(a);
        _scout.Remove(key); _raiders.Remove(key);
        if (_repair.TryAdd(key, tick)) Retreated++;
        if (!k.Army.Remove(a)) k.Detached.Remove(a);
        k.Detached.Add(a);
    }

    /// <summary>VET-6: ticks since the unit was sent to repair; 0 when it was not.</summary>
    public int RepairSince(ViewActor a) => _repair.TryGetValue(RecruitKnowledge.KeyOf(a), out int t) ? t : 0;

    /// <summary>VET-6: a repaired unit rejoins the wave pool at the next <see cref="Apply"/>.</summary>
    public void Release(ViewActor a) => _repair.Remove(RecruitKnowledge.KeyOf(a));

    public void AssignScout(ViewActor a) => _scout.Add(RecruitKnowledge.KeyOf(a));
    public void AssignRaider(ViewActor a) => _raiders.Add(RecruitKnowledge.KeyOf(a));

    /// <summary>Re-sorts the knowledge lists: pooled units stay in Army, squad members move to Home / Detached; dead keys are dropped.</summary>
    public void Apply(RecruitKnowledge k)
    {
        k.Home.Clear(); k.Detached.Clear();
        if (_home.Count == 0 && _scout.Count == 0 && _raiders.Count == 0 && _repair.Count == 0) return;
        PruneRepair(k);
        Prune(_home, k); Prune(_scout, k); Prune(_raiders, k);
        for (int i = k.Army.Count - 1; i >= 0; i--)
        {
            ViewActor a = k.Army[i];
            ulong key = RecruitKnowledge.KeyOf(a);
            if (_home.Contains(key)) { k.Home.Add(a); k.Army.RemoveAt(i); }
            else if (_scout.Contains(key) || _raiders.Contains(key) || _repair.ContainsKey(key)) { k.Detached.Add(a); k.Army.RemoveAt(i); }
        }
        // the lists were filled back to front; restore slot order so every manager sees the same order as before
        k.Home.Reverse(); k.Detached.Reverse();
    }

    /// <summary>Moves a freshly assigned unit out of the pool at once (the same decision it was picked in).</summary>
    public void Detach(RecruitKnowledge k, ViewActor a)
    {
        if (k.Army.Remove(a)) k.Detached.Add(a);
    }

    private void PruneRepair(RecruitKnowledge k)
    {
        if (_repair.Count == 0) return;
        _dead.Clear();
        foreach (ulong key in _repair.Keys)
        {
            bool alive = false;
            foreach (ViewActor a in k.Own) if (RecruitKnowledge.KeyOf(a) == key) { alive = true; break; }
            if (!alive) _dead.Add(key);
        }
        foreach (ulong key in _dead) _repair.Remove(key);
    }

    private void Prune(HashSet<ulong> set, RecruitKnowledge k)
    {
        if (set.Count == 0) return;
        _dead.Clear();
        foreach (ulong key in set)   // order of removal is irrelevant: the set only answers membership
        {
            bool alive = false;
            foreach (ViewActor a in k.Own) if (RecruitKnowledge.KeyOf(a) == key) { alive = true; break; }
            if (!alive) _dead.Add(key);
        }
        foreach (ulong key in _dead) set.Remove(key);
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the three squads (sorted keys) and the retreat counter.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        AiSnapshot.WriteKeys(w, _home); AiSnapshot.WriteKeys(w, _scout); AiSnapshot.WriteKeys(w, _raiders);
        w.Write(Retreated);
        AiSnapshot.WriteMap(w, _repair, (bw, v) => bw.Write(v));   // VET-6
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        AiSnapshot.ReadKeys(r, _home); AiSnapshot.ReadKeys(r, _scout); AiSnapshot.ReadKeys(r, _raiders);
        Retreated = r.ReadInt32();
        AiSnapshot.ReadMap(r, _repair, br => br.ReadInt32());   // VET-6
    }
}
