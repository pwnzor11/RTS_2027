// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;
using Rts.Sim.Rules;

namespace Rts.AI.Recruit;

/// <summary>
/// M3-11 (flag ActiveScouting): one fast unit, kept out of the waves, walks a loop of the places an enemy base can be (the mirror of our
/// start, the centre, the two other corners). A stop is crossed off when the scout is near it or it is in view. What it sees feeds the
/// ordinary last-seen building memory, so waves and superweapons have targets earlier. A lost scout is replaced after <see cref="RepickTicks"/>.
/// </summary>
internal sealed class ScoutManager
{
    /// <summary>Army size before a scout is detached.</summary>
    public const int MinArmy = 6;
    /// <summary>Ticks between scouts.</summary>
    public const int RepickTicks = 400;
    private const int ArriveCells = 3;

    private readonly List<CellCoord> _route = new();
    private int _idx, _nextPick;
    private bool _had;

    /// <summary>Scouts sent out.</summary>
    public int Sent { get; private set; }

    /// <summary>Stops reached.</summary>
    public int Visited { get; private set; }

    public void Start(AiContext ctx)
    {
        int w = ctx.MapWidth, h = ctx.MapHeight, sx = ctx.StartCell.X, sy = ctx.StartCell.Y;
        _route.Add(new CellCoord(w - 1 - sx, h - 1 - sy));
        _route.Add(new CellCoord(w - 1 - sx, sy));
        _route.Add(new CellCoord(w / 2, h / 2));
        _route.Add(new CellCoord(sx, h - 1 - sy));
    }

    public void Think(RecruitKnowledge k, ICommandSink sink, Squads squads)
    {
        int tick = k.View.Tick;
        ViewActor? scout = null;
        foreach (ViewActor a in k.Detached) if (squads.IsScout(a)) { scout = a; break; }
        if (scout is null)
        {
            if (_had) { _had = false; _nextPick = tick + RepickTicks; }
            if (tick < _nextPick || k.Army.Count < MinArmy) return;
            scout = Pick(k);
            if (scout is null) return;
            squads.AssignScout(scout.Value);
            squads.Detach(k, scout.Value);
            Sent++;
            _had = true;
        }
        ViewActor s = scout.Value;
        if (Near(s.Cell, _route[_idx]) || k.View.IsVisible(_route[_idx]))
        {
            Visited++;
            _idx = (_idx + 1) % _route.Count;
        }
        if (s.Idle) sink.Issue(CommandKind.Move, s.Id, s.Kind, _route[_idx]);
    }

    private static bool Near(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= ArriveCells;

    private static ViewActor? Pick(RecruitKnowledge k)
    {
        int best = -1, bestSpeed = -1;
        for (int i = 0; i < k.Army.Count; i++)
        {
            ViewActor a = k.Army[i];
            if (!a.Idle || a.Underground) continue;
            int speed;
            if (a.Kind == ActorKind.Vehicle)
            {
                VehicleType v = k.Rules.Vehicles[a.TypeIndex];
                if (v.Locomotion == Locomotion.Air) continue;
                speed = v.Speed;
            }
            else speed = k.Rules.Infantry[a.TypeIndex].Speed;
            if (speed > bestSpeed) { bestSpeed = speed; best = i; }
        }
        return best < 0 ? null : k.Army[best];
    }
}
