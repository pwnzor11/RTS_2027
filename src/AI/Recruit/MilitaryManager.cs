// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Army control for the Recruit tier. A wave launches when the army reaches the wave's attack threshold (profile threshold
/// plus seeded variation), or (slowly) when a seeded interval of about <see cref="WaveIntervalTicks"/> has passed since the
/// last wave and at least <see cref="MinWave"/> units exist. A wave is a roster: the units that existed at launch (units built
/// later wait at the rally cell for the next wave). The roster first gathers at a staging cell <see cref="StageDistanceCells"/>
/// short of the target so slow infantry and fast tanks arrive together; once most of it is there (or
/// <see cref="StageTimeoutTicks"/> have passed) every idle member attack-moves to the nearest known enemy building, else
/// scouts the places an enemy base can be (the mirror of our start first; fog-honest: the target only changes once we have
/// looked at it). The wave ends when the roster has shrunk to a few units. Defence: if one of our buildings lost health since
/// the last decision, idle units that are not in a wave attack-move to the nearest visible enemy near it. Idle units otherwise
/// gather at the rally cell.
/// </summary>
internal sealed class MilitaryManager
{
    /// <summary>Base ticks between wave launches that are triggered by time rather than army size.</summary>
    public const int WaveIntervalTicks = 3000;
    /// <summary>Smallest army a timed wave will send.</summary>
    public const int MinWave = 8;
    /// <summary>Seeded variation (Recruit only). Each game the AI draws a patience <c>bias</c> of 0..<see cref="MaxBias"/> extra
    /// units that every wave of that game waits for, so two Recruits in one game rarely send equal forces; each wave's army-size
    /// trigger is the profile threshold plus the bias plus up to <see cref="ThresholdJitter"/> - 1 (minus 1), and its timed
    /// trigger comes <see cref="IntervalJitter"/> ticks later at most (plus 200 per bias point).</summary>
    public const int MaxBias = 3, ThresholdJitter = 3, IntervalJitter = 1000;
    /// <summary>Distance (cells) of the staging cell from the target, toward our base.</summary>
    public const int StageDistanceCells = 16;
    /// <summary>Army size at which a side with no scraper left sends everything.</summary>
    public const int DesperateWave = 3;
    /// <summary>Longest a wave waits at the staging cell for stragglers.</summary>
    public const int StageTimeoutTicks = 1800;
    private const int ReissueDistanceCells = 2;
    private const int HomeRadiusCells = 7;
    private const int DefendRadiusCells = 14;
    private const int StageRadiusCells = 5;

    private enum Phase { Gather, Stage, Assault }

    private readonly int _threshold;
    private AiRandom? _rng;
    private int _nextThreshold, _nextInterval = WaveIntervalTicks, _bias;
    private readonly Dictionary<ulong, int> _lastHealth = new();
    private readonly HashSet<ulong> _roster = new();
    private readonly List<CellCoord> _scoutRoute = new();
    private int _scoutIdx;
    private Phase _phase = Phase.Gather;
    private int _lastWave, _stageSince, _launchSize;
    private CellCoord _stage;

    public MilitaryManager(int attackThreshold) => _threshold = Math.Max(1, attackThreshold);

    /// <summary>Waves launched so far.</summary>
    public int Waves { get; private set; }

    /// <summary>Times the defend rule fired.</summary>
    public int Defences { get; private set; }

    public void Start(AiContext ctx)
    {
        _rng = ctx.Rng;
        _bias = _rng.Next(MaxBias + 1);
        Reroll();
        int w = ctx.MapWidth, h = ctx.MapHeight, sx = ctx.StartCell.X, sy = ctx.StartCell.Y;
        _scoutRoute.Add(new CellCoord(w - 1 - sx, h - 1 - sy));   // mirror of our own start
        _scoutRoute.Add(new CellCoord(w / 2, h / 2));
        _scoutRoute.Add(new CellCoord(w - 1 - sx, sy));
        _scoutRoute.Add(new CellCoord(sx, h - 1 - sy));
    }

    /// <summary>Roster members still alive (0 while no wave is on); the roster shrinks with deaths.</summary>
    private int RosterAlive(RecruitKnowledge k)
    {
        if (_phase == Phase.Gather) return 0;
        int alive = 0;
        foreach (ViewActor a in k.Army) if (_roster.Contains(Key(a))) alive++;
        return alive;
    }

    private void Reroll()
    {
        _nextThreshold = Math.Max(2, _threshold + _bias + (_rng?.Next(ThresholdJitter) ?? 0) - 1);
        _nextInterval = WaveIntervalTicks + _bias * 200 + (_rng?.Next(IntervalJitter) ?? 0);
    }

    private static ulong Key(ViewActor a) => ((ulong)a.Id.Index << 16) | a.Id.Generation | ((ulong)(int)a.Kind << 48);

    public void Think(RecruitKnowledge k, ICommandSink sink, CellCoord rally, List<ViewActor> knownEnemyBuildings)
    {
        int tick = k.View.Tick;
        ViewActor? attacked = FindAttacked(k);
        int n = k.Army.Count;

        int alive = RosterAlive(k);

        if (attacked is ViewActor victim)
        {
            CellCoord spot = NearestEnemyTo(k, victim) ?? victim.Cell;
            Defences++;
            foreach (ViewActor a in k.Army)
                if (a.Idle && Chebyshev(a.Cell, spot) > ReissueDistanceCells && !(_phase != Phase.Gather && _roster.Contains(Key(a))))
                    sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, spot);
            if (_phase == Phase.Gather) return;
        }

        if (_phase == Phase.Gather)
        {
            // with no scraper left there is no income to rebuild with: whatever is left goes out as soon as there are a few units
            bool desperate = k.Harvesters.Count == 0 && n >= DesperateWave;
            if (n >= _nextThreshold || (n >= MinWave && tick - _lastWave >= _nextInterval) || desperate)
            {
                _roster.Clear();
                foreach (ViewActor a in k.Army) _roster.Add(Key(a));
                _launchSize = n;
                _lastWave = tick;
                _stageSince = tick;
                Waves++;
                Reroll();
                _stage = StageCell(k, PickGoal(k, knownEnemyBuildings));
                _phase = Phase.Stage;
                alive = n;
            }
        }
        else if (alive < Math.Max(2, _launchSize / 4))
        {
            _phase = Phase.Gather;
            _roster.Clear();
        }

        if (_phase == Phase.Stage)
        {
            int there = 0;
            foreach (ViewActor a in k.Army)
                if (_roster.Contains(Key(a)) && Chebyshev(a.Cell, _stage) <= StageRadiusCells) there++;
            if (there * 100 >= alive * 70 || tick - _stageSince >= StageTimeoutTicks) _phase = Phase.Assault;
        }

        CellCoord goal = _phase == Phase.Assault ? PickGoal(k, knownEnemyBuildings) : default;
        foreach (ViewActor a in k.Army)
        {
            if (!a.Idle) continue;
            bool member = _phase != Phase.Gather && _roster.Contains(Key(a));
            if (member && _phase == Phase.Stage)
            {
                if (Chebyshev(a.Cell, _stage) > ReissueDistanceCells) sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, _stage);
            }
            else if (member)
            {
                if (Chebyshev(a.Cell, goal) > ReissueDistanceCells) sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, goal);
            }
            else if (Chebyshev(a.Cell, rally) > HomeRadiusCells)
                sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        }
    }

    /// <summary>A passable cell <see cref="StageDistanceCells"/> short of <paramref name="goal"/> on the line toward our rally.</summary>
    private CellCoord StageCell(RecruitKnowledge k, CellCoord goal)
    {
        var view = k.View;
        int hx = k.Yard.X, hy = k.Yard.Y;
        long dx = hx - goal.X, dy = hy - goal.Y;
        long len = Math.Max(1, (long)Rts.Sim.Numerics.LeptonMath.ISqrt((ulong)(dx * dx + dy * dy)));
        for (int d = StageDistanceCells; d >= 0; d -= 2)
        {
            int x = (int)Math.Clamp(goal.X + dx * d / len, 1, view.MapWidth - 2), y = (int)Math.Clamp(goal.Y + dy * d / len, 1, view.MapHeight - 2);
            var c = new CellCoord(x, y);
            if (view.IsPassable(c, Locomotion.Tracked)) return c;
        }
        return goal;
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
