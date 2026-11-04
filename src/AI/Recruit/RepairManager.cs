// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (flag Repair): switches repair on for a structure that is below <see cref="StartBelowPct"/> percent health. The sim's Repair
/// command toggles and stops by itself at full health, so the manager remembers which structures it switched on and forgets them when
/// they are whole again. A structure that is still not healing a few decisions later (the sim stops repairs when the player is broke)
/// is toggled once more.
/// </summary>
internal sealed class RepairManager
{
    /// <summary>Health percent below which repair is started.</summary>
    public const int StartBelowPct = 75;
    /// <summary>Decisions without healing before a repair is toggled again.</summary>
    public const int StuckDecisions = 4;

    private readonly Dictionary<ulong, (int Health, int Stuck)> _on = new();
    private readonly List<ulong> _forget = new();

    /// <summary>Repair orders given.</summary>
    public int Orders { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink)
    {
        if (_on.Count > 0)
        {
            _forget.Clear();
            foreach (ViewActor b in k.Buildings)
            {
                ulong key = RecruitKnowledge.KeyOf(b);
                if (b.Health >= b.MaxHealth && _on.ContainsKey(key)) _forget.Add(key);
            }
            foreach (ulong key in _forget) _on.Remove(key);
        }
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            if (bt.Wall || bt.Gate || b.Health >= b.MaxHealth) continue;
            ulong key = RecruitKnowledge.KeyOf(b);
            if (_on.TryGetValue(key, out var st))
            {
                if (b.Health > st.Health) { _on[key] = (b.Health, 0); continue; }
                if (st.Stuck + 1 < StuckDecisions) { _on[key] = (b.Health, st.Stuck + 1); continue; }
                _on[key] = (b.Health, 0);   // not healing: toggle again
                Orders++;
                sink.Issue(CommandKind.Repair, b.Id, ActorKind.Building);
                continue;
            }
            if (b.Health * 100 >= b.MaxHealth * StartBelowPct) continue;
            _on[key] = (b.Health, 0);
            Orders++;
            sink.Issue(CommandKind.Repair, b.Id, ActorKind.Building);
        }
    }
}
