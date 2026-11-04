// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (flag Scatter): splash is recognised by its signature, two or more of our units within <see cref="ClusterCells"/> cells of each
/// other that each lost at least <see cref="HitPct"/> percent of their health since the last decision while standing still. Those
/// units get the Scatter command (the sim moves each to a free neighbouring cell), at most once per <see cref="CooldownTicks"/> per unit.
/// </summary>
internal sealed class ScatterManager
{
    /// <summary>Cells within which damaged units count as a cluster.</summary>
    public const int ClusterCells = 2;
    /// <summary>Health percent of max a unit must lose between two decisions.</summary>
    public const int HitPct = 6;
    /// <summary>Ticks before the same unit is scattered again.</summary>
    public const int CooldownTicks = 60;

    private readonly Dictionary<ulong, int> _health = new();
    private readonly Dictionary<ulong, int> _last = new();
    private readonly List<ViewActor> _hit = new();

    /// <summary>Scatter orders given.</summary>
    public int Orders { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
        int tick = k.View.Tick;
        _hit.Clear();
        if (_health.Count > 600) { _health.Clear(); _last.Clear(); }   // dead units' keys: start over rather than grow
        foreach (ViewActor a in k.Army)
        {
            ulong key = RecruitKnowledge.KeyOf(a);
            bool had = _health.TryGetValue(key, out int prev);
            _health[key] = a.Health;
            if (!had || !a.Idle || a.Underground || prev - a.Health < a.MaxHealth * HitPct / 100) continue;
            if (_last.TryGetValue(key, out int at) && tick - at < CooldownTicks) continue;
            _hit.Add(a);
        }
        foreach (ViewActor a in _hit)
        {
            bool cluster = false;
            foreach (ViewActor b in _hit)
                if (b.Id != a.Id && Math.Max(Math.Abs(a.Cell.X - b.Cell.X), Math.Abs(a.Cell.Y - b.Cell.Y)) <= ClusterCells) { cluster = true; break; }
            if (!cluster) continue;
            _last[RecruitKnowledge.KeyOf(a)] = tick;
            Orders++;
            sink.Issue(CommandKind.Scatter, a.Id, a.Kind);
        }
    }
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the health memory and the cooldowns.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        AiSnapshot.WriteMap(w, _health, (bw, v) => bw.Write(v));
        AiSnapshot.WriteMap(w, _last, (bw, v) => bw.Write(v));
        w.Write(Orders);
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        AiSnapshot.ReadMap(r, _health, br => br.ReadInt32());
        AiSnapshot.ReadMap(r, _last, br => br.ReadInt32());
        Orders = r.ReadInt32();
    }
}
