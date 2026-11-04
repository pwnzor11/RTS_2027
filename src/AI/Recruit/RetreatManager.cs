// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (profile retreat_health_pct above 0): a unit below that health percent leaves the fight, walks to the rally cell and joins the
/// home guard (see <see cref="Squads"/>). Units do not heal in the field, so a retreated unit stays home and defends. Scouts and raiders
/// retreat the same way.
/// </summary>
internal sealed class RetreatManager
{
    private readonly List<ViewActor> _tmp = new();

    /// <summary>Units sent home.</summary>
    public int Count { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink, Squads squads, CellCoord rally, int pct)
    {
        if (pct <= 0) return;
        _tmp.Clear();
        foreach (ViewActor a in k.Army) if (Weak(a, pct)) _tmp.Add(a);
        foreach (ViewActor a in k.Detached) if (Weak(a, pct)) _tmp.Add(a);
        foreach (ViewActor a in _tmp)
        {
            squads.SendHome(k, a);
            Count++;
            sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        }
    }

    private static bool Weak(ViewActor a, int pct) => !a.Underground && a.MaxHealth > 0 && a.Health * 100 < a.MaxHealth * pct;
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the counter.</summary>
    internal void Snapshot(BinaryWriter w) => w.Write(Count);

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r) => Count = r.ReadInt32();
}
