// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (flag Harass): a small raiding party of the fastest idle army units, kept out of the waves (see <see cref="Squads"/>). It hunts
/// the enemy harvester last seen within the profile's scout_info_age_ticks (attacking it by id while it is in view, attack-moving to where
/// it was otherwise), and failing that the nearest known enemy power structure. With no target the party waits at the rally cell. When
/// the party is wiped out a new one is picked after <see cref="RepickTicks"/>.
/// </summary>
internal sealed class HarassManager
{
    /// <summary>Raiders in a party.</summary>
    public const int PartySize = 3;
    /// <summary>Army size before a party is formed (the waves need a body first).</summary>
    public const int MinArmy = 8;
    /// <summary>Ticks between forming parties.</summary>
    public const int RepickTicks = 600;

    private int _nextPick;
    private int _lastCount;

    /// <summary>Parties formed.</summary>
    public int Parties { get; private set; }

    /// <summary>Attack orders issued by raiders.</summary>
    public int Strikes { get; private set; }

    public void Think(RecruitKnowledge k, ICommandSink sink, Squads squads, TierPlanner planner, List<ViewActor> knownEnemyBuildings,
        CellCoord rally, int infoAge)
    {
        int tick = k.View.Tick;
        int have = squads.RaiderCount;
        if (have == 0 && _lastCount > 0) _nextPick = tick + RepickTicks;
        if (have < PartySize && k.Army.Count >= MinArmy && tick >= _nextPick) Recruit(k, squads, PartySize - have);
        have = squads.RaiderCount;
        _lastCount = have;
        if (have == 0) return;

        CellCoord cell = default;
        ActorRef target = default;
        bool live = false;
        bool found = planner.TryHarvesterTarget(k, infoAge, out cell, out target, out live);
        if (!found) found = NearestPower(k, knownEnemyBuildings, out cell);
        foreach (ViewActor a in k.Detached)
        {
            if (!squads.IsRaider(a) || !a.Idle) continue;
            if (!found)
            {
                if (Math.Max(Math.Abs(a.Cell.X - rally.X), Math.Abs(a.Cell.Y - rally.Y)) > 6) sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
            }
            else if (live)
            {
                Strikes++;
                sink.Issue(CommandKind.Attack, a.Id, a.Kind, default, target);
            }
            else if (Math.Max(Math.Abs(a.Cell.X - cell.X), Math.Abs(a.Cell.Y - cell.Y)) > 2)
            {
                Strikes++;
                sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, cell);
            }
        }
    }

    private void Recruit(RecruitKnowledge k, Squads squads, int want)
    {
        SimRules rules = k.Rules;
        for (int n = 0; n < want; n++)
        {
            int best = -1, bestSpeed = -1;
            for (int i = 0; i < k.Army.Count; i++)
            {
                ViewActor a = k.Army[i];
                if (!a.Idle || a.Underground) continue;
                int speed;
                if (a.Kind == ActorKind.Vehicle)
                {
                    VehicleType v = rules.Vehicles[a.TypeIndex];
                    if (v.Locomotion == Locomotion.Air) continue;
                    speed = v.Speed;
                }
                else speed = rules.Infantry[a.TypeIndex].Speed;
                if (speed > bestSpeed) { bestSpeed = speed; best = i; }
            }
            if (best < 0) return;
            ViewActor pick = k.Army[best];
            squads.AssignRaider(pick);
            squads.Detach(k, pick);
            if (n == 0) Parties++;
        }
    }

    /// <summary>The known enemy structure that produces power nearest to our yard.</summary>
    private static bool NearestPower(RecruitKnowledge k, List<ViewActor> buildings, out CellCoord cell)
    {
        cell = default;
        long best = long.MaxValue;
        bool found = false;
        foreach (ViewActor b in buildings)
        {
            if (k.Rules.Buildings[b.TypeIndex].Power <= 0) continue;
            long dx = b.Cell.X - k.Yard.X, dy = b.Cell.Y - k.Yard.Y, d = dx * dx + dy * dy;
            if (d >= best) continue;
            best = d; cell = b.Cell; found = true;
        }
        return found;
    }
}
