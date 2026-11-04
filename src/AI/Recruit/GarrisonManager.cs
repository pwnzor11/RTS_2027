// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M4-12 (flag RespondToAttacks, tier 2+): fills garrisonable buildings near the base with idle armed infantry (design 08 §3): our own
/// Blockhouse / Redoubt (the build order adds one, see <see cref="TierPlanner.ExtendBuild"/>) and empty neutral ruins in view within
/// <see cref="RadiusCells"/> of the yard. Fog-honest: occupancy of own buildings comes from <see cref="PlayerView.OccupantsOf"/>; a
/// neutral ruin is only tried while it is in view, and soldiers already walking in are counted from our own order memory. Soldiers that
/// were ordered in are taken out of the army list at once so no other manager re-orders them in the same decision.
/// </summary>
internal sealed class GarrisonManager
{
    /// <summary>Cells from the yard a garrisonable building may be to be filled.</summary>
    public const int RadiusCells = 14;   // (tune)
    /// <summary>Soldiers kept out of garrisons so the army still has a body.</summary>
    public const int KeepInArmy = 4;   // (tune)
    /// <summary>Ticks an Enter order is remembered (a soldier walking in counts toward the building).</summary>
    public const int WalkTicks = 400;
    /// <summary>Most Enter orders per decision.</summary>
    public const int PerDecision = 2;

    private readonly List<(ulong Soldier, EntityId Building, int Tick)> _walking = new();

    /// <summary>Enter orders given to fill a garrison.</summary>
    public int Orders { get; private set; }

    /// <summary>Most soldiers seen inside own garrisons at once.</summary>
    public int PeakOccupants { get; private set; }

    /// <summary>Unload orders given (a side with no income empties its garrisons for the last attack).</summary>
    public int Unloads { get; private set; }

    private readonly List<(EntityId Building, int Tick)> _unloaded = new();

    /// <summary>With <paramref name="allIn"/> (no harvester and no money for one: the side can only win by attacking now) every own
    /// occupied garrison is emptied and none is filled, so the soldiers join the last wave instead of waiting out the clock.</summary>
    public void Think(RecruitKnowledge k, ICommandSink sink, bool allIn = false)
    {
        if (!k.HaveYard) return;
        int tick = k.View.Tick;
        if (allIn)
        {
            _unloaded.RemoveAll(u => tick - u.Tick > WalkTicks);
            foreach (ViewActor b in k.Buildings)
            {
                if (k.Rules.Buildings[b.TypeIndex].Garrison <= 0 || k.View.OccupantsOf(b.Id) <= 0) continue;
                bool recent = false;
                foreach (var u in _unloaded) if (u.Building == b.Id) recent = true;
                if (recent) continue;
                _unloaded.Add((b.Id, tick));
                Unloads++;
                sink.Issue(CommandKind.Unload, b.Id, ActorKind.Building);
            }
            return;
        }
        _walking.RemoveAll(w => tick - w.Tick > WalkTicks);
        if (k.Garrisoned.Count > PeakOccupants) PeakOccupants = k.Garrisoned.Count;
        int budget = PerDecision;
        int infantry = 0;
        foreach (ViewActor a in k.Army) if (a.Kind == ActorKind.Infantry) infantry++;

        // own buildings first (slot order), then neutral ruins in view (slot order)
        for (int pass = 0; pass < 2 && budget > 0; pass++)
        {
            List<ViewActor> list = pass == 0 ? k.Buildings : k.Visible;
            for (int bi = 0; bi < list.Count && budget > 0; bi++)
            {
                ViewActor b = list[bi];
                if (b.Kind != ActorKind.Building) continue;
                if (pass == 1 && b.Relation != Relation.Neutral) continue;
                BuildingType bt = k.Rules.Buildings[b.TypeIndex];
                if (bt.Garrison <= 0 || Cheb(b.Cell, k.Yard) > RadiusCells) continue;
                int inside = pass == 0 ? Math.Max(0, k.View.OccupantsOf(b.Id)) : 0;
                int coming = 0;
                foreach (var w in _walking) if (w.Building == b.Id) coming++;
                int room = bt.Garrison - inside - coming;
                while (room > 0 && budget > 0 && infantry > KeepInArmy)
                {
                    int pick = Nearest(k, b);
                    if (pick < 0) return;
                    ViewActor s = k.Army[pick];
                    sink.Issue(CommandKind.Enter, s.Id, s.Kind, default, new ActorRef(ActorKind.Building, b.Id));
                    _walking.Add((RecruitKnowledge.KeyOf(s), b.Id, tick));
                    k.Army.RemoveAt(pick);
                    Orders++;
                    room--; budget--; infantry--;
                }
            }
        }
    }

    /// <summary>Index in the army of the idle armed soldier (able to garrison) nearest the building, or -1.</summary>
    private static int Nearest(RecruitKnowledge k, ViewActor b)
    {
        int best = -1, bestD = int.MaxValue;
        for (int i = 0; i < k.Army.Count; i++)
        {
            ViewActor a = k.Army[i];
            if (a.Kind != ActorKind.Infantry || !a.Idle || a.Underground) continue;
            InfantryType it = k.Rules.Infantry[a.TypeIndex];
            if (!it.CanGarrison || it.PrimaryWeapon < 0) continue;
            int d = Cheb(a.Cell, b.Cell);
            if (d > RadiusCells + 4 || d >= bestD) continue;
            bestD = d; best = i;
        }
        return best;
    }

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
