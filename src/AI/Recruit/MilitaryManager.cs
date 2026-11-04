// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
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
    public const int GroupRetreatMarginPct = 10;

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
    // M4-12: target player (2-4 players), re-chosen every TargetReviewTicks; -1 = none known
    private int _targetPlayer = -1, _targetSince = int.MinValue;
    private int _player;
    private CellCoord _home;
    private readonly List<ViewActor> _targetBuildings = new();
    private readonly int[] _knownCount = new int[16];
    private readonly long[] _knownDist = new long[16];

    /// <summary>M4-12: ticks between re-choosing the enemy player waves go for (design 08 §2: 30 s).</summary>
    public const int TargetReviewTicks = 600;   // (tune)
    /// <summary>M4-12: a wave in Assault that has not destroyed anything for this long is ended (its members then join the next one).</summary>
    public const int StaleAssaultTicks = 2400;   // (tune)

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
    public CellCoord Goal(RecruitKnowledge k, List<ViewActor> knownEnemyBuildings) =>
        PickGoal(k, _targetPlayer >= 0 ? _targetBuildings : knownEnemyBuildings);

    /// <summary>Waves launched so far.</summary>
    public int Waves { get; private set; }

    /// <summary>Times the defend rule fired.</summary>
    public int Defences { get; private set; }

    public void Start(AiContext ctx)
    {
        _rng = ctx.Rng;
        _bias = _rng.Next(MaxBias + 1);
        Reroll();
        _player = ctx.Player;
        _home = ctx.StartCell;
        _scoutRoute.AddRange(StartRoute.For(ctx));   // M4-12: every other start of a 2-4 player map, nearest first, then centre and corners
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

    /// <summary>M4-12 (diagnostics): the wave phase (0 gather, 1 stage, 2 assault), the scout-route stop and the last goal picked.</summary>
    public string Debug => $"{(int)_phase}:{_scoutIdx}/{_scoutRoute.Count}@{(_scoutRoute.Count > 0 ? _scoutRoute[Math.Min(_scoutIdx, _scoutRoute.Count - 1)] : default)} goal {_lastGoal} stage {_stage} roster {_roster.Count}";

    /// <summary>M4-12: set by the AI while its income has stalled (map mined out): waves go out at <see cref="DesperateWave"/> units.</summary>
    public bool Broke { get; set; }

    /// <summary>M4-12: the enemy player waves go for (-1 while no enemy building is known).</summary>
    public int TargetPlayer => _targetPlayer;

    /// <summary>M4-12: fresh units that triggered a regroup (a new wave while the old one was still on the attack).</summary>
    public int Reinforcements { get; private set; }

    /// <summary>M4-12: waves ended because they stalled (nothing destroyed for <see cref="StaleAssaultTicks"/>).</summary>
    public int StaleWaves { get; private set; }

    private int _progressTick, _progressScore = int.MaxValue;

    /// <summary>
    /// M4-12: keeps <see cref="_targetBuildings"/> = the known buildings of the enemy player waves go for. The player is re-chosen
    /// every <see cref="TargetReviewTicks"/> (or when nothing of it is known any more): the nearest one to our start (distance to its
    /// nearest known building), with a player that has fewer known buildings counted nearer (weakest first when distances are close).
    /// Ties go to the lower slot. Fog-honest: only known (live or ghost) buildings count.
    /// </summary>
    private void ChooseTarget(RecruitKnowledge k, List<ViewActor> known)
    {
        int tick = k.View.Tick;
        Array.Clear(_knownCount);
        Array.Fill(_knownDist, long.MaxValue);
        foreach (ViewActor b in known)
        {
            if (b.Owner >= _knownCount.Length) continue;
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            if (bt.Wall || bt.Gate) continue;
            _knownCount[b.Owner]++;
            long d = Chebyshev(b.Cell, _home);
            if (d < _knownDist[b.Owner]) _knownDist[b.Owner] = d;
        }
        bool keep = _targetPlayer >= 0 && _knownCount[_targetPlayer] > 0 && tick - _targetSince < TargetReviewTicks;
        if (!keep)
        {
            int best = -1, candidates = 0;
            long bestScore = long.MaxValue;
            for (int p = 0; p < _knownCount.Length; p++) if (p != _player && _knownCount[p] > 0) candidates++;
            for (int p = 0; p < _knownCount.Length; p++)
            {
                if (p == _player || _knownCount[p] == 0) continue;
                // distance first (cells x 4); fewer known buildings = weaker; with several candidates a seeded 0..7 jitter so equidistant
                // neighbours are not always resolved by slot order (no number is drawn with one candidate: 2-player games are unchanged)
                long score = _knownDist[p] * 4 + Math.Min(_knownCount[p], 20) + (candidates > 1 ? _rng?.Next(8) ?? 0 : 0);
                if (score < bestScore) { bestScore = score; best = p; }
            }
            if (best != _targetPlayer) _progressScore = int.MaxValue;
            _targetPlayer = best;
            _targetSince = tick;
        }
        _targetBuildings.Clear();
        foreach (ViewActor b in known)
            if (_targetPlayer < 0 || b.Owner == _targetPlayer) _targetBuildings.Add(b);
    }

    public void Think(RecruitKnowledge k, ICommandSink sink, CellCoord rally, List<ViewActor> allKnownEnemyBuildings)
    {
        int tick = k.View.Tick;
        ChooseTarget(k, allKnownEnemyBuildings);
        List<ViewActor> knownEnemyBuildings = _targetBuildings;
        ViewActor? attacked = FindAttacked(k);
        int n = k.Army.Count;

        int alive = RosterAlive(k);

        // M4-12: a harvester only counts as attacked with a visible enemy near it (radiation also lowers its health)
        if (attacked is ViewActor hv && hv.Kind != ActorKind.Building && NearestEnemyTo(k, hv) is null) attacked = null;
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
            bool desperate = (k.Harvesters.Count == 0 || Broke) && n >= DesperateWave;   // M4-12: also when income has stalled
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
                _progressScore = int.MaxValue; _progressTick = tick;
            }
        }
        else if (alive < Math.Max(2, _launchSize / 4))
        {
            _phase = Phase.Gather;
            _roster.Clear();
        }
        else if (Options.Respond && _phase == Phase.Assault && Stalled(k, knownEnemyBuildings))
        {
            // M4-12: the wave has destroyed nothing for a long time (a target it cannot kill, a wall, free repairs): end it; every
            // survivor is counted again for the next wave, which goes out at once when the army is big enough
            StaleWaves++;
            _phase = Phase.Gather;
            _roster.Clear(); _squadB.Clear();
            _prong = false;
            _lastWave = tick - _nextInterval;   // the timed trigger is due at once
        }
        else if (Options.Respond && _phase == Phase.Assault && Regroup(k))
        {
            // M4-12: enough fresh units wait at home for a full wave: end this one so the next launches with survivors and fresh units
            _phase = Phase.Gather;
            _roster.Clear(); _squadB.Clear();
            _prong = false;
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

        if (Options.Respond) Unstick(k, sink);
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

    /// <summary>
    /// M4-12 (finishing games): true when the idle units at home that are not in the wave reach the next wave's threshold. In M3 a wave
    /// that kept a few survivors alive at the enemy base never ended, so every later unit waited at the rally for the rest of the game.
    /// Ending the wave instead of trickling reinforcements in keeps the army massed (a trickle was fed to the defence piecemeal).
    /// </summary>
    private bool Regroup(RecruitKnowledge k)
    {
        int waiting = 0;
        foreach (ViewActor a in k.Army)
            if (a.Idle && !_roster.Contains(Key(a)) && !a.Underground) waiting++;
        if (waiting < _nextThreshold) return false;
        Reinforcements += waiting;
        return true;
    }

    /// <summary>M4-12: true when the known buildings of the target have not lost health between two decisions for
    /// <see cref="StaleAssaultTicks"/> (a target the wave cannot reach or cannot hurt). The goal it was attacking is then avoided for
    /// <see cref="AvoidTicks"/>.</summary>
    private bool Stalled(RecruitKnowledge k, List<ViewActor> known)
    {
        long score = 0;
        foreach (ViewActor b in known) score += b.Health + 1;
        int tick = k.View.Tick;
        int sc = (int)Math.Min(score, int.MaxValue);
        bool progress = sc < _progressScore;
        _progressScore = sc;
        if (progress) { _progressTick = tick; return false; }
        if (tick - _progressTick < StaleAssaultTicks) return false;
        _avoid.Add((_lastGoal, tick + AvoidTicks));
        return true;
    }

    /// <summary>M4-12: how long a goal a stalled wave could not hurt is skipped.</summary>
    public const int AvoidTicks = 6000;   // (tune)
    private readonly List<(CellCoord Cell, int Until)> _avoid = new();
    private CellCoord _lastGoal;

    private bool Avoided(CellCoord c, int tick)
    {
        for (int i = _avoid.Count - 1; i >= 0; i--)
        {
            if (_avoid[i].Until <= tick) { _avoid.RemoveAt(i); continue; }
            if (_avoid[i].Cell == c) return true;
        }
        return false;
    }

    /// <summary>M4-12: ticks a busy army unit may stand on one cell with no enemy in view near it before it is stopped.</summary>
    public const int StuckTicks = 400;   // (tune)
    private const int StuckEnemyCells = 8;
    private readonly Dictionary<ulong, (CellCoord Cell, int Since, int Times)> _still = new();
    private readonly List<ulong> _stillDead = new();

    /// <summary>M4-12: units unstuck by the watchdog.</summary>
    public int Unstuck { get; private set; }

    /// <summary>
    /// M4-12 (finishing games): an attack-move whose destination cell can never be reached (the footprint of one of our own buildings, a
    /// cell walled off by our own base) never ends, so the unit is never idle again and no manager re-orders it; whole waves froze like
    /// that. A busy unit that has not changed cell for <see cref="StuckTicks"/> with no visible enemy near it is stopped (it becomes idle
    /// and is ordered afresh next decision); a unit stuck twice in a row is first moved to a seeded nearby cell.
    /// </summary>
    private void Unstick(RecruitKnowledge k, ICommandSink sink)
    {
        int tick = k.View.Tick;
        _stillDead.Clear();
        foreach (ulong key in _still.Keys) _stillDead.Add(key);   // order irrelevant: only used to drop dead keys
        foreach (ViewActor a in k.Army)
        {
            ulong key = Key(a);
            _stillDead.Remove(key);
            if (a.Idle || a.Underground || a.Mission is MissionKind.EnterTunnel or MissionKind.Capture or MissionKind.Deploy
                || a.Kind == ActorKind.Vehicle && k.Rules.Vehicles[a.TypeIndex].Locomotion == Locomotion.Air)
            { _still.Remove(key); continue; }
            if (!_still.TryGetValue(key, out var st) || st.Cell != a.Cell) { _still[key] = (a.Cell, tick, st.Cell == a.Cell ? st.Times : 0); continue; }
            if (tick - st.Since < StuckTicks || EnemyNear(k, a.Cell)) continue;
            Unstuck++;
            if (st.Times >= 1 && _rng is not null)
            {
                var c = new CellCoord(Math.Clamp(a.Cell.X + _rng.Next(7) - 3, 1, k.View.MapWidth - 2), Math.Clamp(a.Cell.Y + _rng.Next(7) - 3, 1, k.View.MapHeight - 2));
                sink.Issue(CommandKind.Move, a.Id, a.Kind, c);
            }
            else sink.Issue(CommandKind.Stop, a.Id, a.Kind);
            _still[key] = (a.Cell, tick, st.Times + 1);
        }
        foreach (ulong key in _stillDead) _still.Remove(key);
    }

    private static bool EnemyNear(RecruitKnowledge k, CellCoord c)
    {
        foreach (ViewActor e in k.Visible)
            if (e.Relation == Relation.Enemy && Chebyshev(e.Cell, c) <= StuckEnemyCells) return true;
        return false;
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
        int tick = k.View.Tick;
        for (int pass = 0; pass < 2 && !found; pass++)
            foreach (ViewActor b in buildings)
            {
                if (pass == 0 && _avoid.Count > 0 && Avoided(b.Cell, tick)) continue;   // M4-12: skip goals a stalled wave could not hurt
                long dx = b.Position.X - centre.X, dy = b.Position.Y - centre.Y, d = dx * dx + dy * dy;
                if (d >= best) continue;
                best = d; goal = b.Cell; found = true;
            }
        if (found) { _lastGoal = goal; return goal; }
        // nothing known: look at the route stops in turn; a stop that is in view and shows no building is crossed off
        while (_scoutIdx < _scoutRoute.Count - 1 && k.View.IsVisible(_scoutRoute[_scoutIdx])) _scoutIdx++;
        if (_scoutIdx >= _scoutRoute.Count - 1 && k.View.IsVisible(_scoutRoute[^1])) _scoutIdx = 0;
        return _scoutRoute[_scoutIdx];
    }

    /// <summary>The own building (in slot order) whose health fell since the previous decision, if any; with the Respond option
    /// (M4-12) also a harvester that is being shot (losing the only harvester was the most common way games froze).</summary>
    private ViewActor? FindAttacked(RecruitKnowledge k)
    {
        ViewActor? hit = null;
        foreach (ViewActor b in k.Buildings)
        {
            ulong key = ((ulong)b.Id.Index << 16) | b.Id.Generation;
            if (hit is null && _lastHealth.TryGetValue(key, out int prev) && b.Health < prev) hit = b;
            _lastHealth[key] = b.Health;
        }
        if (!Options.Respond) return hit;
        foreach (ViewActor h in k.Harvesters)
        {
            ulong key = RecruitKnowledge.KeyOf(h) | (1UL << 62);   // buildings use (index, generation) with no kind bits: keep apart
            if (hit is null && !h.Underground && _lastHealth.TryGetValue(key, out int prev) && h.Health < prev) hit = h;
            _lastHealth[key] = h.Health;
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
