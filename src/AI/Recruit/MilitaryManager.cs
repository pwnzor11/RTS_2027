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

    /// <summary>Smallest wave that splits into two prongs.</summary>
    public const int MinProngArmy = 12;
    /// <summary>Cells apart two targets must be to count as separate prongs.</summary>
    public const int ProngSeparationCells = 10;
    /// <summary>Ticks after a called-off wave before the next may start.</summary>
    public const int RetreatCooldownTicks = 600;
    /// <summary>A wave is called off when its health is below (retreat percent + this) percent of its health at launch.</summary>
    public const int GroupRetreatMarginPct = 20;

    private readonly int _threshold;
    private AiRandom? _rng;
    private int _nextThreshold, _nextInterval = WaveIntervalTicks, _bias;
    private readonly Dictionary<ulong, int> _lastHealth = new();
    private readonly HashSet<ulong> _roster = new();
    private readonly List<CellCoord> _scoutRoute = new();
    private int _scoutIdx;
    private Phase _phase = Phase.Gather;
    private int _lastWave, _stageSince, _launchSize;
    private CellCoord _stage, _stageB;
    private readonly HashSet<ulong> _squadB = new();
    private bool _prong;
    private long _launchHealth;
    private int _cooldownUntil;

    public MilitaryManager(int attackThreshold) => _threshold = Math.Max(1, attackThreshold);

    /// <summary>M3-11 capabilities (from the profile). The default is the original Recruit behaviour: defend, one prong, never retreat.</summary>
    public MilitaryOptions Options { get; set; } = MilitaryOptions.Recruit;

    /// <summary>Waves called off because they had lost too much strength.</summary>
    public int GroupRetreats { get; private set; }

    /// <summary>Waves that went out as two prongs.</summary>
    public int Prongs { get; private set; }

    /// <summary>Faction extras (M3-10): forward staging and tunnel transit for the wave. Null for Authority (behaviour unchanged).</summary>
    public IFactionTactics? Tactics { get; set; }

    /// <summary>Mean position of the wave members alive while a wave is on the march, else null.</summary>
    public LeptonCoord? WaveCentre(RecruitKnowledge k)
    {
        if (_phase == Phase.Gather) return null;
        long x = 0, y = 0;
        int n = 0;
        foreach (ViewActor a in k.Army)
            if (_roster.Contains(Key(a))) { x += a.Position.X; y += a.Position.Y; n++; }
        return n == 0 ? null : new LeptonCoord((int)(x / n), (int)(y / n));
    }

    /// <summary>The cell the army is heading for now (nearest known enemy building, else the scouting route).</summary>
    public CellCoord Goal(RecruitKnowledge k, List<ViewActor> knownEnemyBuildings) => PickGoal(k, knownEnemyBuildings);

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
            if (Options.Respond)   // M3-11: the improved response also calls the home guard (tier 1 keeps the original rule)
            foreach (ViewActor a in k.Home)
                if (a.Idle && Chebyshev(a.Cell, spot) > ReissueDistanceCells)
                    sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, spot);
            if (_phase == Phase.Gather) return;
        }

        if (_phase != Phase.Gather && Options.RetreatPct > 0 && _launchHealth > 0 && CallOff(k, sink, rally))
        {
            alive = 0;
            n = k.Army.Count;
        }

        if (_phase == Phase.Gather)
        {
            // with no scraper left there is no income to rebuild with: whatever is left goes out as soon as there are a few units
            bool desperate = k.Harvesters.Count == 0 && n >= DesperateWave;
            if (tick >= _cooldownUntil && (n >= _nextThreshold || (n >= MinWave && tick - _lastWave >= _nextInterval) || desperate))
            {
                _roster.Clear(); _squadB.Clear();
                long hp = 0;
                int idx = 0;
                CellCoord first = PickGoal(k, knownEnemyBuildings);
                CellCoord? second = Options.TwoPronged && n >= MinProngArmy ? SecondGoal(k, knownEnemyBuildings, first) : null;
                _prong = second is not null;
                foreach (ViewActor a in k.Army)
                {
                    _roster.Add(Key(a));
                    hp += a.Health;
                    if (_prong && (idx++ & 1) == 1) _squadB.Add(Key(a));
                }
                if (_prong) Prongs++;
                _launchHealth = hp;
                _launchSize = n;
                _lastWave = tick;
                _stageSince = tick;
                Waves++;
                Reroll();
                CellCoord launchGoal = PickGoal(k, knownEnemyBuildings);
                _stage = Tactics?.ForwardStage(k, launchGoal) ?? StageCell(k, launchGoal);
                if (Tactics != null) _stage = Tactics.SafeCell(k, _stage);
                _stageB = _stage;
                if (_prong && Options.MultiFront && second is CellCoord flank)
                {
                    _stageB = StageCell(k, flank);   // the second prong approaches from its own side and the two assault together
                    if (Tactics != null) _stageB = Tactics.SafeCell(k, _stageB);
                }
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
            int there = 0, thereB = 0, aliveB = 0;
            foreach (ViewActor a in k.Army)
            {
                ulong key = Key(a);
                if (!_roster.Contains(key)) continue;
                bool b = _squadB.Contains(key);
                if (b) aliveB++;
                if (Chebyshev(a.Cell, b ? _stageB : _stage) > StageRadiusCells) continue;
                if (b) thereB++; else there++;
            }
            int aliveA = alive - aliveB;
            bool ready = there * 100 >= aliveA * 70 && thereB * 100 >= aliveB * 70;
            if (!_prong) ready = (there + thereB) * 100 >= alive * 70;
            if (ready || tick - _stageSince >= StageTimeoutTicks) _phase = Phase.Assault;
        }

        CellCoord goal = _phase == Phase.Assault ? PickGoal(k, knownEnemyBuildings) : default;
        CellCoord goalB = goal;
        if (_prong && _phase == Phase.Assault) goalB = SecondGoal(k, knownEnemyBuildings, goal) ?? goal;
        foreach (ViewActor a in k.Army)
        {
            if (!a.Idle) continue;
            bool member = _phase != Phase.Gather && _roster.Contains(Key(a));
            bool prongB = member && _squadB.Contains(Key(a));
            if (member && _phase == Phase.Stage)
            {
                CellCoord stage = prongB ? _stageB : _stage;
                if (Tactics != null && Tactics.TryEnter(k, sink, a, stage)) continue;
                if (Chebyshev(a.Cell, stage) > ReissueDistanceCells) sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, stage);
            }
            else if (member)
            {
                CellCoord target = prongB ? goalB : goal;
                if (Chebyshev(a.Cell, target) > ReissueDistanceCells) sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, target);
            }
            else if (Chebyshev(a.Cell, rally) > HomeRadiusCells)
                sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        }
    }

    /// <summary>Calls the wave off when its health fell below the retreat line: every roster member walks home. True when it did.</summary>
    private bool CallOff(RecruitKnowledge k, ICommandSink sink, CellCoord rally)
    {
        long now = 0;
        foreach (ViewActor a in k.Army) if (_roster.Contains(Key(a))) now += a.Health;
        if (now * 100 >= _launchHealth * (Options.RetreatPct + GroupRetreatMarginPct)) return false;
        foreach (ViewActor a in k.Army)
            if (_roster.Contains(Key(a))) sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        _roster.Clear(); _squadB.Clear();
        _phase = Phase.Gather;
        _prong = false;
        _cooldownUntil = k.View.Tick + RetreatCooldownTicks;
        GroupRetreats++;
        return true;
    }

    /// <summary>The known enemy building nearest the army that is at least <see cref="ProngSeparationCells"/> from <paramref name="first"/>, or null.</summary>
    private CellCoord? SecondGoal(RecruitKnowledge k, List<ViewActor> buildings, CellCoord first)
    {
        long cx = 0, cy = 0;
        foreach (ViewActor a in k.Army) { cx += a.Position.X; cy += a.Position.Y; }
        var centre = k.Army.Count == 0 ? default : new LeptonCoord((int)(cx / k.Army.Count), (int)(cy / k.Army.Count));
        long best = long.MaxValue;
        CellCoord? found = null;
        foreach (ViewActor b in buildings)
        {
            if (Chebyshev(b.Cell, first) < ProngSeparationCells) continue;
            long dx = b.Position.X - centre.X, dy = b.Position.Y - centre.Y, d = dx * dx + dy * dy;
            if (d >= best) continue;
            best = d; found = b.Cell;
        }
        return found;
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
