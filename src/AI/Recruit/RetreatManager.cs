// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (profile retreat_health_pct above 0): a unit below that health percent leaves the fight, walks to the rally cell and joins the
/// home guard (see <see cref="Squads"/>); VET-6: +10 percent per veterancy rank, and a rank-3 unit goes to repair and rejoins instead. Units do not heal in the field, so a retreated unit stays home and defends. Scouts and raiders
/// retreat the same way.
/// </summary>
internal sealed class RetreatManager
{
    private readonly List<ViewActor> _tmp = new();

    /// <summary>Units sent home.</summary>
    public int Count { get; private set; }

    /// <summary>VET-6: extra retreat percent per veterancy rank (rank 2-3 units leave earlier).</summary>
    public const int RankPct = 10;   // (tune) design 14 section 6b

    /// <summary>VET-6: a repairing rank-3 unit rejoins the army at this health percent.</summary>
    public const int RepairDonePct = 90;   // (tune)

    /// <summary>VET-6: a repairing unit rejoins after this many ticks whatever its health (no waiting for ever).</summary>
    public const int RepairMaxTicks = 3600;   // (tune)

    /// <summary>M7-10: every retreating unit goes to repair (a Mender) and rejoins when healed, not only rank-3 ones (Expedition: few, dear units).</summary>
    public bool RepairAll { get; set; }

    public void Think(RecruitKnowledge k, ICommandSink sink, Squads squads, CellCoord rally, int pct)
    {
        if (pct <= 0) return;
        int tick = k.View.Tick;
        foreach (ViewActor a in k.Detached)   // VET-6: healed (or waited long enough) rank-3 units go back to the pool
            if (squads.IsRepairing(a) && (a.Health * 100 >= a.MaxHealth * Math.Max(RepairDonePct, Math.Min(100, Threshold(a, pct) + 5))
                || tick - squads.RepairSince(a) >= RepairMaxTicks)) squads.Release(a);
        _tmp.Clear();
        foreach (ViewActor a in k.Army) if (Weak(a, pct)) _tmp.Add(a);
        foreach (ViewActor a in k.Detached) if (!squads.IsRepairing(a) && Weak(a, pct)) _tmp.Add(a);
        foreach (ViewActor a in _tmp)
        {
            Count++;
            if (a.Rank >= VeterancyValue.MaxRank || RepairAll)
            {
                squads.SendRepair(k, a, tick);
                sink.Issue(CommandKind.Move, a.Id, a.Kind, RepairSpot(k, a, rally));
                continue;
            }
            squads.SendHome(k, a);
            sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        }
    }

    /// <summary>VET-6: where a retreated rank-3 unit repairs. The sim has no unit repair building, so a vehicle goes to the nearest own
    /// repair-aura carrier (Support list); otherwise to the rally cell, where a rank-3 unit heals itself while it stays out of combat.</summary>
    private static CellCoord RepairSpot(RecruitKnowledge k, ViewActor a, CellCoord rally)
    {
        if (a.Kind != ActorKind.Vehicle) return rally;
        long best = long.MaxValue;
        CellCoord spot = rally;
        foreach (ViewActor s in k.Support)
        {
            if (s.Kind != ActorKind.Vehicle || k.Rules.Vehicles[s.TypeIndex].AuraRepairPermille <= 0) continue;
            long dx = s.Cell.X - a.Cell.X, dy = s.Cell.Y - a.Cell.Y, d = dx * dx + dy * dy;
            if (d >= best) continue;
            best = d; spot = s.Cell;
        }
        return spot;
    }

    private static int Threshold(ViewActor a, int pct) => Math.Min(100, pct + RankPct * Math.Clamp(a.Rank, 0, VeterancyValue.MaxRank));

    private static bool Weak(ViewActor a, int pct) => !a.Underground && a.MaxHealth > 0 && a.Health * 100 < a.MaxHealth * Threshold(a, pct);
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the counter.</summary>
    internal void Snapshot(BinaryWriter w) => w.Write(Count);

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r) => Count = r.ReadInt32();
}
