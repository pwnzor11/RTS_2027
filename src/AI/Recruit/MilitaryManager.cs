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
    private bool _prong, _aheadWave;
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

    /// <summary>M4-16: staging distance (cells short of the target) while the army is clearly ahead of the known enemy army.</summary>
    public const int AheadStageCells = 6;   // (tune) M4-16 (was 16 for every wave)
    /// <summary>M4-16: longest an ahead wave waits at its staging cell.</summary>
    public const int AheadStageTimeoutTicks = 600;   // (tune) M4-16 (was 1800)
    /// <summary>M4-16: map width (cells) from which the regroup threshold is shortened.</summary>
    public const int LargeMapCells = 80;   // (tune) M4-16
    /// <summary>M4-16: percent of the next wave's threshold the idle units at home must reach to regroup on a large map.</summary>
    public const int LargeMapRegroupPct = 60;   // (tune) M4-16
    /// <summary>M4-16: ticks between target reviews with 3-4 players (after a kill the next victim is chosen sooner).</summary>
    public const int MultiTargetReviewTicks = 300;   // (tune) M4-16 (was 600)
    /// <summary>M4-16: weight of a known building in the target score with several candidates (distance cells count 4); weakest-first.</summary>
    public const int CountWeight = 3;   // (tune) M4-16 (was 1)

    /// <summary>M5-02: the commander's directive as knob values, set by the AI each decision (<see cref="DirectiveBias.Plain"/> = no directive).</summary>
    public DirectiveBias Bias { get; set; } = DirectiveBias.Plain;

    /// <summary>M4-16: set by the AI each decision: our army is clearly bigger than the enemy army seen lately (stage closer, hit production first).</summary>
    public bool Ahead { get; set; }

    /// <summary>M4-16 round 4: credit value of our army and of the biggest enemy army seen lately (set by the AI each decision).</summary>
    public int OwnValue { get; set; }
    public int EnemyValueSeen { get; set; }

    private int _mapWidth, _startCount;
    private int _launchValue, _required;

    // VET-6: rank-aware army values (design 14 section 6a). Peaks of the enemy army value seen at once, short and long window, with the
    // part that came from rank (bonus). While no rank is involved (veterancy off: bonus 0) the values the AI passes in are used unchanged.
    private int _eShortVal, _eShortBonus, _eShortTick = int.MinValue, _eLongVal, _eLongBonus, _eLongTick = int.MinValue;

    /// <summary>
    /// VET-6: adds the veterancy bonus (cost x 250 x rank / 1000 per unit) to <see cref="OwnValue"/>, <see cref="EnemyValueSeen"/> and
    /// <see cref="Ahead"/>. Does nothing while no ranked unit is in view, so rank 0 everywhere changes no decision.
    /// </summary>
    private void ApplyRankValues(RecruitKnowledge k)
    {
        var rules = k.Rules;
        int ownBase = 0, own = 0, enBase = 0, en = 0;
        foreach (ViewActor a in k.Own)
            if (a.IsMobile && !(a.Kind == ActorKind.Vehicle && rules.Vehicles[a.TypeIndex].HarvestCapacity > 0))
            { own += VeterancyValue.Of(rules, a); ownBase += VeterancyValue.Of(rules, a with { Rank = 0 }); }
        foreach (ViewActor e in k.Visible)
            if (e.Relation == Relation.Enemy && e.IsMobile && !(e.Kind == ActorKind.Vehicle && rules.Vehicles[e.TypeIndex].HarvestCapacity > 0))
            { en += VeterancyValue.Of(rules, e); enBase += VeterancyValue.Of(rules, e with { Rank = 0 }); }
        int now = k.View.Tick;
        int enBonus = en - enBase;
        if (en >= _eShortVal || now - _eShortTick > TierPlanner.EnemyValueMemoryTicks) { _eShortVal = en; _eShortBonus = enBonus; _eShortTick = now; }
        if (en >= _eLongVal || now - _eLongTick > TierPlanner.WaveGateMemoryTicks) { _eLongVal = en; _eLongBonus = enBonus; _eLongTick = now; }
        int ownBonus = own - ownBase;
        if (ownBonus == 0 && _eShortBonus == 0 && _eLongBonus == 0) return;
        OwnValue += ownBonus;
        EnemyValueSeen += _eLongBonus;
        // the planner's Ahead used unranked values: with ranks in play, decide it again from the ranked ones
        Ahead = k.Army.Count >= TierPlanner.AheadMinUnits && (long)(ownBase + ownBonus) * 100 >= (long)_eShortVal * (100 + TierPlanner.AheadMarginPct);
    }

    /// <summary>M4-16 round 4: a wave that lost most of its roster raises the army value the next one waits for.</summary>
    private void WaveFailed()
    {
        _required = WaveGate.Escalate(Options.Respond, _required, _launchValue);
    }

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
        _mapWidth = ctx.MapWidth; _startCount = ctx.StartPositions.Count;
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
        // M5-02: the commander's target, when it is another player and the view knows a building of it; else the AI chooses
        int forced = Bias.TargetPlayer;
        if (forced < 0 || forced >= _knownCount.Length || forced == _player || _knownCount[forced] == 0) forced = -1;
        bool keep = _targetPlayer >= 0 && _knownCount[_targetPlayer] > 0 && tick - _targetSince < (_startCount > 2 && Options.Respond ? MultiTargetReviewTicks : TargetReviewTicks);
        if (forced >= 0)
        {
            if (forced != _targetPlayer) { _progressScore = int.MaxValue; _targetPlayer = forced; _targetSince = tick; }
        }
        else if (!keep)
        {
            int best = -1, candidates = 0;
            long bestScore = long.MaxValue;
            for (int p = 0; p < _knownCount.Length; p++) if (p != _player && _knownCount[p] > 0) candidates++;
            for (int p = 0; p < _knownCount.Length; p++)
            {
                if (p == _player || _knownCount[p] == 0) continue;
                // distance first (cells x 4); fewer known buildings = weaker; with several candidates a seeded 0..7 jitter so equidistant
                // neighbours are not always resolved by slot order (no number is drawn with one candidate: 2-player games are unchanged)
                long score = _knownDist[p] * 4 + Math.Min(_knownCount[p], 20) * (candidates > 1 && Options.Respond ? CountWeight : 1) + (candidates > 1 ? _rng?.Next(8) ?? 0 : 0);
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
        ApplyRankValues(k);   // VET-6
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

        int retreatPct = Bias.RetreatPct(Options.RetreatPct);   // M5-02
        if (_phase != Phase.Gather && retreatPct > 0 && _launchHealth > 0 && CallOff(k, sink, rally, retreatPct))
        {
            alive = 0;
            n = k.Army.Count;
        }

        if (_phase == Phase.Gather)
        {
            // with no scraper left there is no income to rebuild with: whatever is left goes out as soon as there are a few units
            bool desperate = (k.Harvesters.Count == 0 || Broke) && n >= DesperateWave;   // M4-12: also when income has stalled
            int guard = Bias.HomeGuard;   // M5-02: units that stay home (the wave waits for them too)
            bool strong = WaveGate.Strong(Options.Respond, OwnValue, EnemyValueSeen, _required, n, tick, _lastWave);   // M4-16 r4: no wave into a clearly bigger army
            if (tick >= _cooldownUntil && (strong || desperate) && (n >= Bias.Wave(_nextThreshold, 2) + guard
                || (n >= Bias.Wave(MinWave, 2) + guard && tick - _lastWave >= Bias.Wave(_nextInterval, 600)) || desperate))
            {
                _roster.Clear(); _squadB.Clear();
                long hp = 0;
                int idx = 0;
                CellCoord first = PickGoal(k, knownEnemyBuildings);
                CellCoord? second = Options.TwoPronged && n >= MinProngArmy ? SecondGoal(k, knownEnemyBuildings, first) : null;
                _prong = second is not null;
                int take = desperate ? n : Math.Max(1, n - guard), seen = 0;
                foreach (ViewActor a in k.Army)
                {
                    if (seen++ >= take) break;
                    _roster.Add(Key(a));
                    hp += a.Health;
                    if (_prong && (idx++ & 1) == 1) _squadB.Add(Key(a));
                }
                if (_prong) Prongs++;
                _launchHealth = hp;
                _launchSize = take;
                _launchValue = OwnValue;
                _lastWave = tick;
                _stageSince = tick;
                Waves++;
                Reroll();
                CellCoord launchGoal = PickGoal(k, knownEnemyBuildings);
                _aheadWave = Options.Respond && Ahead;
                _stage = Tactics?.ForwardStage(k, launchGoal) ?? StageCell(k, launchGoal, _aheadWave ? AheadStageCells : StageDistanceCells);
                if (Tactics != null) _stage = Tactics.SafeCell(k, _stage);
                _stageB = _stage;
                if (_prong && Options.MultiFront && second is CellCoord flank)
                {
                    _stageB = StageCell(k, flank, _aheadWave ? AheadStageCells : StageDistanceCells);   // the second prong approaches from its own side and the two assault together
                    if (Tactics != null) _stageB = Tactics.SafeCell(k, _stageB);
                }
                _phase = Phase.Stage;
                alive = take;
                _progressScore = int.MaxValue; _progressTick = tick;
            }
        }
        else if (alive < Math.Max(2, _launchSize / 4))
        {
            WaveFailed();
            _phase = Phase.Gather;
            _roster.Clear();
        }
        else if (Options.Respond && _phase == Phase.Assault && Stalled(k, knownEnemyBuildings))
        {
            // M4-12: the wave has destroyed nothing for a long time (a target it cannot kill, a wall, free repairs): end it; every
            // survivor is counted again for the next wave, which goes out at once when the army is big enough
            StaleWaves++;
            _required = 0;
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
            if (ready || tick - _stageSince >= (_aheadWave ? AheadStageTimeoutTicks : StageTimeoutTicks)) _phase = Phase.Assault;
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
        int threshold = Bias.Wave(_nextThreshold, 2);   // M5-02
        int need = (_mapWidth >= LargeMapCells ? Math.Max(2, threshold * LargeMapRegroupPct / 100) : threshold) + Bias.HomeGuard;   // M4-16
        if (waiting < need) return false;
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

    /// <summary>M5-02: cells from the commander's attack point at which the army counts as arrived.</summary>
    public const int FocusArrivedCells = 6;

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
    private bool CallOff(RecruitKnowledge k, ICommandSink sink, CellCoord rally, int retreatPct)
    {
        long now = 0;
        foreach (ViewActor a in k.Army) if (_roster.Contains(Key(a))) now += a.Health;
        if (now * 100 >= _launchHealth * (retreatPct + GroupRetreatMarginPct)) return false;
        foreach (ViewActor a in k.Army)
            if (_roster.Contains(Key(a))) sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        _roster.Clear(); _squadB.Clear();
        WaveFailed();
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
    private CellCoord StageCell(RecruitKnowledge k, CellCoord goal, int distance)
    {
        var view = k.View;
        int hx = k.Yard.X, hy = k.Yard.Y;
        long dx = hx - goal.X, dy = hy - goal.Y;
        long len = Math.Max(1, (long)Rts.Sim.Numerics.LeptonMath.ISqrt((ulong)(dx * dx + dy * dy)));
        for (int d = distance; d >= 0; d -= 2)
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
        // M5-02: the commander's attack point comes first until the army stands near it (or a wave there stalled), then the usual goals
        if (Bias.HasAttackCell && k.Army.Count > 0 && !Avoided(Bias.AttackCell, k.View.Tick)
            && Chebyshev(new CellCoord(centre.X / 256, centre.Y / 256), Bias.AttackCell) > FocusArrivedCells)
        { _lastGoal = Bias.AttackCell; return Bias.AttackCell; }
        long best = long.MaxValue;
        CellCoord goal = default;
        bool found = false;
        int tick = k.View.Tick;
        bool priority = Options.Respond && Ahead;
        for (int pass = priority ? -1 : 0; pass < 2 && !found; pass++)
            foreach (ViewActor b in buildings)
            {
                if (pass == -1)   // M4-16: while winning, production and income structures first (the enemy cannot rebuild an army without them)
                {
                    BuildingType bt = k.Rules.Buildings[b.TypeIndex];
                    if (bt.Produces == null && !bt.Refinery) continue;
                }
                if (pass <= 0 && _avoid.Count > 0 && Avoided(b.Cell, tick)) continue;   // M4-12: skip goals a stalled wave could not hurt
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
    // ---- M3-21: AI save/restore. Appended at the end of the class; the format is private to the matching Restore. ----

    /// <summary>M3-21: writes the wave state: phase, target player, roster and squads, staging cells, thresholds, route position, avoid list,
    /// stuck watchdog and the counters. The scout route and the home cell are rebuilt by Start from the saved start cell.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_nextThreshold); w.Write(_nextInterval); w.Write(_bias);
        AiSnapshot.WriteMap(w, _lastHealth, (bw, v) => bw.Write(v));
        AiSnapshot.WriteKeys(w, _roster);
        w.Write(_scoutIdx); w.Write((byte)_phase);
        w.Write(_lastWave); w.Write(_stageSince); w.Write(_launchSize);
        AiSnapshot.WriteCell(w, _stage); AiSnapshot.WriteCell(w, _stageB);
        AiSnapshot.WriteKeys(w, _squadB);
        w.Write(_prong); w.Write(_launchHealth); w.Write(_cooldownUntil);
        w.Write(_targetPlayer); w.Write(_targetSince);
        w.Write(_progressTick); w.Write(_progressScore);
        w.Write(_avoid.Count);
        foreach (var a in _avoid) { AiSnapshot.WriteCell(w, a.Cell); w.Write(a.Until); }
        AiSnapshot.WriteCell(w, _lastGoal);
        AiSnapshot.WriteMap(w, _still, (bw, v) => { AiSnapshot.WriteCell(bw, v.Cell); bw.Write(v.Since); bw.Write(v.Times); });
        w.Write(GroupRetreats); w.Write(Prongs); w.Write(Waves); w.Write(Defences); w.Write(Reinforcements); w.Write(StaleWaves); w.Write(Unstuck);
        w.Write(_aheadWave);   // M4-16
        w.Write(_launchValue); w.Write(_required);   // M4-16 r4
        w.Write(_eShortVal); w.Write(_eShortBonus); w.Write(_eShortTick); w.Write(_eLongVal); w.Write(_eLongBonus); w.Write(_eLongTick);   // VET-6
    }

    /// <summary>M3-21: reads what <see cref="Snapshot"/> wrote. Call after Start.</summary>
    internal void Restore(BinaryReader r)
    {
        _nextThreshold = r.ReadInt32(); _nextInterval = r.ReadInt32(); _bias = r.ReadInt32();
        AiSnapshot.ReadMap(r, _lastHealth, br => br.ReadInt32());
        AiSnapshot.ReadKeys(r, _roster);
        _scoutIdx = r.ReadInt32(); _phase = (Phase)r.ReadByte();
        _lastWave = r.ReadInt32(); _stageSince = r.ReadInt32(); _launchSize = r.ReadInt32();
        _stage = AiSnapshot.ReadCell(r); _stageB = AiSnapshot.ReadCell(r);
        AiSnapshot.ReadKeys(r, _squadB);
        _prong = r.ReadBoolean(); _launchHealth = r.ReadInt64(); _cooldownUntil = r.ReadInt32();
        _targetPlayer = r.ReadInt32(); _targetSince = r.ReadInt32();
        _progressTick = r.ReadInt32(); _progressScore = r.ReadInt32();
        int n = AiSnapshot.Count(r);
        _avoid.Clear();
        for (int i = 0; i < n; i++) _avoid.Add((AiSnapshot.ReadCell(r), r.ReadInt32()));
        _lastGoal = AiSnapshot.ReadCell(r);
        AiSnapshot.ReadMap(r, _still, br => (AiSnapshot.ReadCell(br), br.ReadInt32(), br.ReadInt32()));
        GroupRetreats = r.ReadInt32(); Prongs = r.ReadInt32(); Waves = r.ReadInt32(); Defences = r.ReadInt32(); Reinforcements = r.ReadInt32();
        StaleWaves = r.ReadInt32(); Unstuck = r.ReadInt32();
        _aheadWave = r.ReadBoolean();   // M4-16
        _launchValue = r.ReadInt32(); _required = r.ReadInt32();   // M4-16 r4
        _eShortVal = r.ReadInt32(); _eShortBonus = r.ReadInt32(); _eShortTick = r.ReadInt32(); _eLongVal = r.ReadInt32(); _eLongBonus = r.ReadInt32(); _eLongTick = r.ReadInt32();   // VET-6
        if (_scoutRoute.Count > 0) _scoutIdx = Math.Clamp(_scoutIdx, 0, _scoutRoute.Count - 1);
    }
}
