// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Army control for the Recruit tier. A wave launches when the army reaches the profile's attack threshold, or (slowly)
/// when <see cref="WaveIntervalTicks"/> have passed since the last wave and at least <see cref="MinWave"/> units exist.
/// While a wave is on, every idle unit attack-moves to the nearest known enemy building, else scouts the places an enemy
/// base can be (the mirror of our start first; fog-honest: the target only changes once we have looked at it). The wave
/// ends when the army has shrunk to a few units. Defence: if one of our buildings lost health since the last decision, idle
/// units attack-move to the nearest visible enemy near it. Idle units otherwise gather at the rally cell.
/// </summary>
internal sealed class MilitaryManager
{
    /// <summary>Minimum ticks between wave launches that are triggered by time rather than army size.</summary>
    public const int WaveIntervalTicks = 4800;
    /// <summary>Smallest army a timed wave will send.</summary>
    public const int MinWave = 6;
    private const int ReissueDistanceCells = 2;
    private const int HomeRadiusCells = 7;
    private const int DefendRadiusCells = 14;

    private readonly int _threshold;
    private readonly Dictionary<ulong, int> _lastHealth = new();
    private readonly List<CellCoord> _scoutRoute = new();
    private int _scoutIdx;
    private bool _attacking;
    private int _lastWave;

    public MilitaryManager(int attackThreshold) => _threshold = Math.Max(1, attackThreshold);

    /// <summary>Waves launched so far.</summary>
    public int Waves { get; private set; }

    /// <summary>Times the defend rule fired.</summary>
    public int Defences { get; private set; }

    public void Start(AiContext ctx)
    {
        int w = ctx.MapWidth, h = ctx.MapHeight, sx = ctx.StartCell.X, sy = ctx.StartCell.Y;
        _scoutRoute.Add(new CellCoord(w - 1 - sx, h - 1 - sy));   // mirror of our own start
        _scoutRoute.Add(new CellCoord(w / 2, h / 2));
        _scoutRoute.Add(new CellCoord(w - 1 - sx, sy));
        _scoutRoute.Add(new CellCoord(sx, h - 1 - sy));
    }

    public void Think(RecruitKnowledge k, ICommandSink sink, CellCoord rally, List<ViewActor> knownEnemyBuildings)
    {
        int tick = k.View.Tick;
        ViewActor? attacked = FindAttacked(k);
        int n = k.Army.Count;

        if (attacked is ViewActor victim)
        {
            CellCoord spot = NearestEnemyTo(k, victim) ?? victim.Cell;
            Defences++;
            foreach (ViewActor a in k.Army)
                if (a.Idle && Chebyshev(a.Cell, spot) > ReissueDistanceCells)
                    sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, spot);
            return;
        }

        if (!_attacking)
        {
            if (n >= _threshold || (n >= MinWave && tick - _lastWave >= WaveIntervalTicks))
            {
                _attacking = true;
                _lastWave = tick;
                Waves++;
            }
        }
        else if (n < Math.Max(2, _threshold / 4)) _attacking = false;

        if (_attacking)
        {
            CellCoord goal = PickGoal(k, knownEnemyBuildings);
            foreach (ViewActor a in k.Army)
                if (a.Idle && Chebyshev(a.Cell, goal) > ReissueDistanceCells)
                    sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, goal);
        }
        else
        {
            foreach (ViewActor a in k.Army)
                if (a.Idle && Chebyshev(a.Cell, rally) > HomeRadiusCells)
                    sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        }
    }

    private CellCoord PickGoal(RecruitKnowledge k, List<ViewActor> buildings)
    {
        long cx = 0, cy = 0;
        foreach (ViewActor a in k.Army) { cx += a.Position.X; cy += a.Position.Y; }
        var centre = k.Army.Count == 0 ? default : new LeptonCoord((int)(cx / k.Army.Count), (int)(cy / k.Army.Count));
        long best = long.MaxValue;
        CellCoord goal = default;
        bool found = false;
        foreach (ViewActor b in buildings)
        {
            long dx = b.Position.X - centre.X, dy = b.Position.Y - centre.Y, d = dx * dx + dy * dy;
            if (d >= best) continue;
            best = d; goal = b.Cell; found = true;
        }
        if (found) return goal;
        // nothing known: look at the route stops in turn; a stop that is in view and shows no building is crossed off
        while (_scoutIdx < _scoutRoute.Count - 1 && k.View.IsVisible(_scoutRoute[_scoutIdx])) _scoutIdx++;
        if (_scoutIdx >= _scoutRoute.Count - 1 && k.View.IsVisible(_scoutRoute[^1])) _scoutIdx = 0;
        return _scoutRoute[_scoutIdx];
    }

    /// <summary>The own building (in slot order) whose health fell since the previous decision, if any.</summary>
    private ViewActor? FindAttacked(RecruitKnowledge k)
    {
        ViewActor? hit = null;
        foreach (ViewActor b in k.Buildings)
        {
            ulong key = ((ulong)b.Id.Index << 16) | b.Id.Generation;
            if (hit is null && _lastHealth.TryGetValue(key, out int prev) && b.Health < prev) hit = b;
            _lastHealth[key] = b.Health;
        }
        return hit;
    }

    private static CellCoord? NearestEnemyTo(RecruitKnowledge k, ViewActor victim)
    {
        long best = (long)DefendRadiusCells * DefendRadiusCells;
        CellCoord? found = null;
        foreach (ViewActor e in k.Visible)
        {
            if (e.Relation != Relation.Enemy) continue;
            long dx = e.Cell.X - victim.Cell.X, dy = e.Cell.Y - victim.Cell.Y, d = dx * dx + dy * dy;
            if (d > best) continue;
            if (found is not null && d == best) continue;
            best = d; found = e.Cell;
        }
        return found;
    }

    private static int Chebyshev(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}
