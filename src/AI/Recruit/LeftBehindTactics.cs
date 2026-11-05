// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M6-10: what the Left Behind AI does beyond the shared loop (the Raider Warlord, design 06 §8). Fog-honest: it reads only its
/// <see cref="PlayerView"/> (wrecks and storms are public) and the order history it keeps itself.
/// <list type="bullet">
/// <item>Unpack: the base starts as a packed Mother Rig (and any packed rig that stands idle is unpacked again): a legal site near it,
/// beside a wreck field when there is one.</item>
/// <item>Raids: idle Buggies and Hook Runners (up to a party cap that grows with the tier) are taken out of the wave pool and sent to
/// plunder the nearest known enemy Silo / Refinery; with none known they probe the enemy start positions. Hurt raiders go home for good.</item>
/// <item>Salvage: idle Haulers (and Tinkers near the base) go to the richest safe wreck within reach, slot order on ties; a Hauler is
/// bought once wrecks lie about.</item>
/// <item>Relocation (profile tier 3+): under a heavy attack on the base the packable rigs pack, drive to the fallback (the richest wreck
/// field or own storm away from the enemy) and unpack; the fixed structures left behind are sold. A cooldown stops thrash.</item>
/// <item>Superweapon: The Second Drift is fired at the enemy's biggest army cluster, else the densest enemy base cluster, then the raiders
/// are chained onto the nearest enemy Silo. Dust Sails join the production at the top tiers.</item>
/// </list>
/// </summary>
internal sealed class LeftBehindTactics : IFactionTactics
{
    /// <summary>Raiders kept out of the waves at once, by profile tier (1-2, 3, 4-5).</summary>
    public const int PartyLow = 4, PartyMid = 6, PartyTop = 8;   // (tune) M6-10
    /// <summary>Idle raiders at home before a party sets out.</summary>
    public const int PartyStart = 3;   // (tune) M6-10
    /// <summary>A raider below this health percent goes home for good.</summary>
    public const int RaiderHurtPct = 40;   // (tune) M6-10
    /// <summary>Cells in which a salvager looks for a wreck.</summary>
    public const int SalvageReach = 36;   // (tune) M6-10
    /// <summary>Cells in which an idle Tinker near the base looks for a wreck.</summary>
    public const int TinkerReach = 12;   // (tune) M6-10
    /// <summary>Wrecks within this many cells of a seen enemy gun are not salvaged.</summary>
    public const int WreckDangerCells = 8;   // (tune) M6-10
    /// <summary>Total wreck value in view before a second Hauler (tier 3+) or an early one is bought.</summary>
    public const int WreckValueForHauler = 60;   // (tune) session 10 (was 150)
    /// <summary>Own army size at which the first Hauler is bought even before any wreck lies about (it then waits for the first fight).</summary>
    public const int ArmyForFirstHauler = 5;   // (tune) session 10
    /// <summary>Ticks after the last Hauler died before another is bought.</summary>
    public const int HaulerRebuildTicks = 3600;   // (tune) session 10
    /// <summary>Funds that must be on hand (on top of the 500 price) before a Hauler is bought.</summary>
    public const int HaulerCushion = 900;   // (tune) session 10
    /// <summary>Ticks between Return orders to an idle Hauler that found no wreck.</summary>
    public const int ReturnGapTicks = 400;   // (tune) M6-10

    /// <summary>Cells from the base within which seen enemies count as an attack.</summary>
    public const int ThreatCells = 12;   // (tune) M6-10
    /// <summary>Credit value of enemy gunmen near the base that counts as a heavy attack.</summary>
    public const int HeavyThreat = 900;   // (tune) M6-10
    /// <summary>The enemy must be worth this percent of the army at home.</summary>
    public const int ThreatOverArmyPct = 150;   // (tune) M6-10
    /// <summary>Consecutive decisions the attack must last before the rigs pack.</summary>
    public const int ThreatStreakNeeded = 3;   // (tune) M6-10
    /// <summary>Ticks after a relocation (or an abandoned one) before the next may start.</summary>
    public const int RelocateCooldownTicks = 4800;   // (tune) M6-10
    /// <summary>Ticks after which a relocation that has not finished is abandoned.</summary>
    public const int RelocateTimeoutTicks = 3600;   // (tune) M6-10
    /// <summary>The fallback must be at least this many cells from the base it leaves.</summary>
    public const int RelocateMinCells = 14;   // (tune) M6-10
    /// <summary>Cells the plan retreats from the threat when no wreck field or storm offers a better place.</summary>
    public const int RelocateAwayCells = 22;   // (tune) M6-10
    /// <summary>Fixed structures farther than this from the new Mother Rig are sold after a relocation.</summary>
    public const int SellBeyondCells = 16;   // (tune) M6-10

    /// <summary>Cells within which enemy units count as one cluster for the superweapon.</summary>
    public const int ClusterCells = 5;   // (tune) M6-10
    /// <summary>Credit value an army cluster must have to be worth the Second Drift.</summary>
    public const int ArmyClusterValue = 700;   // (tune) M6-10
    /// <summary>Known enemy structures in one cluster that make a base worth the Second Drift.</summary>
    public const int BaseClusterMin = 3;   // (tune) M6-10
    /// <summary>Ticks between Special orders.</summary>
    public const int FireGapTicks = 300;   // (tune) M6-10
    /// <summary>Ticks the raiders are chained onto the storm's neighbourhood after the Second Drift.</summary>
    public const int ChainTicks = 600;   // (tune) M6-10
    /// <summary>Army size before a Dust Sail is bought (top tiers).</summary>
    public const int ArmyBeforeSail = 6;   // (tune) M6-10
    /// <summary>Dust Sails wanted at once (top tiers).</summary>
    public const int SailsWanted = 2;   // (tune) M6-10

    private enum Reloc : byte { Idle = 0, Moving = 1 }

    private readonly DifficultyProfile? _profile;
    private Squads? _squads;
    private int _mapW, _mapH;
    private CellCoord _home;
    private readonly List<CellCoord> _probes = new();
    private int _probeIdx;

    // resolved ids (rules are fixed per game)
    private bool _resolved;
    private int _hauler = -1, _sail = -1, _gleaner = -1, _mother = -1;

    private readonly List<WreckView> _wrecks = new();
    private readonly List<StormView> _storms = new();
    private int _wreckValueNow;
    private int _haulerSeen = -100000;   // last tick a Hauler was alive or queued

    /// <summary>SUP-5: packed rigs that were given an unpack order, with the tick of the order. One still standing idle 60 ticks after its
    /// order means the order ended at once (unreachable site): the next order unpacks it near where it stands.</summary>
    private readonly List<(ulong Key, int Tick, int X, int Y)> _ordered = new();
    /// <summary>SUP-5: unpack sites whose order ended at once (top-left, expiry tick); not chosen again until they expire.</summary>
    private readonly List<(int X, int Y, int Until)> _bad = new();
    private const int OrderWaitTicks = 60;

    // relocation
    private Reloc _reloc;
    private int _relocStart, _relocCooldownUntil;
    private CellCoord _dest, _oldAnchor;
    private bool _seenPacked;
    private int _threatStreak;

    // superweapon / chain
    private int _lastFire = -FireGapTicks;
    private CellCoord _chain;
    private int _chainUntil = -1;

    private readonly Dictionary<ulong, int> _lastReturn = new();
    private readonly List<ulong> _drop = new();
    private readonly List<(int X0, int Y0, int X1, int Y1)> _reserved = new();
    private readonly List<(long Score, int X, int Y)> _cands = new();

    /// <summary>Creates the tactics for a tier's profile (null: the lowest tier).</summary>
    public LeftBehindTactics(DifficultyProfile? profile) => _profile = profile;

    private int Tier => _profile?.Tier ?? 1;

    /// <summary>Diagnostics: the last threat reading.</summary>
    public string Debug { get; private set; } = "";
    /// <summary>Unpack orders given.</summary>
    public int Unpacks { get; private set; }
    /// <summary>Raid parties sent (a group of idle raiders given a target).</summary>
    public int RaidParties { get; private set; }
    /// <summary>Raid orders given (Enter on a Silo / Refinery, or an attack-move to where one stood).</summary>
    public int RaidOrders { get; private set; }
    /// <summary>Salvage orders given.</summary>
    public int SalvageOrders { get; private set; }
    /// <summary>Relocations started.</summary>
    public int Relocations { get; private set; }
    /// <summary>Relocations that ended with the rigs unpacked at the fallback.</summary>
    public int RelocationsDone { get; private set; }
    /// <summary>Relocations abandoned (timeout).</summary>
    public int RelocationsAborted { get; private set; }
    /// <summary>Structures sold after a relocation.</summary>
    public int Sold { get; private set; }
    /// <summary>Second Drift orders given.</summary>
    public int StormOrders { get; private set; }
    /// <summary>True while a relocation is under way.</summary>
    public bool Relocating => _reloc != Reloc.Idle;
    /// <summary>The fallback cell of the current (or last) relocation.</summary>
    public CellCoord RelocationTarget => _dest;

    /// <inheritdoc />
    public void Start(AiContext ctx)
    {
        _mapW = ctx.MapWidth; _mapH = ctx.MapHeight; _home = ctx.StartCell;
        // enemy start guesses: every other start position, nearest first (ties by map order)
        var others = new List<CellCoord>();
        foreach (CellCoord s in ctx.StartPositions) if (Cheb(s, ctx.StartCell) > 6) others.Add(s);
        for (int i = 0; i < others.Count; i++)
        {
            int best = i;
            for (int j = i + 1; j < others.Count; j++)
                if (Dist2(others[j], ctx.StartCell) < Dist2(others[best], ctx.StartCell)) best = j;
            (others[i], others[best]) = (others[best], others[i]);
        }
        _probes.Clear(); _probes.AddRange(others);
        if (_probes.Count == 0) _probes.Add(new CellCoord(_mapW / 2, _mapH / 2));
    }

    /// <summary>Hands over the squads (raiders are taken out of the wave pool).</summary>
    public void Attach(Squads squads) => _squads = squads;

    private void Resolve(SimRules rules)
    {
        if (_resolved) return;
        _resolved = true;
        _hauler = SimRules.IndexOf(rules.Vehicles, "lb_hauler", v => v.Id);
        _sail = SimRules.IndexOf(rules.Vehicles, "lb_dust_sail", v => v.Id);
        _gleaner = SimRules.IndexOf(rules.Vehicles, "lb_gleaner", v => v.Id);
        _mother = SimRules.IndexOf(rules.Buildings, "lb_mother_rig", b => b.Id);
    }

    // ---- IFactionTactics --------------------------------------------------------------------------------

    /// <inheritdoc />
    public void Think(RecruitKnowledge k, ICommandSink sink, CellCoord goal, CellCoord rally, LeptonCoord? waveCentre)
    {
        Resolve(k.Rules);
        if (Tier >= 3) Relocate(k, sink);
        if (_profile is { Superweapons: true }) FireStorm(k, sink);
    }

    /// <inheritdoc />
    public int WantedVehicle(RecruitKnowledge k)
    {
        Resolve(k.Rules);
        if (!k.HaveYard) return -1;
        QueueView q = k.Queue(ProductionCategory.Vehicle);
        // a Hauler once wrecks lie about (and an income to wait for)
        if (_hauler >= 0 && k.Harvesters.Count > 0 && k.Buildable(ProductionCategory.Vehicle, _hauler))
        {
            int have = k.Salvagers.Count, cap = Tier >= 3 ? 2 : 1;
            foreach (QueueItemView it in q.Items) if (it.TypeIndex == _hauler) have++;
            // session 10: one Hauler early (once a small army stands and the first harvester works), so it is at hand when the first
            // wrecks fall; the second (tier 3+) waits for wrecks to lie about
            // a Hauler costs 500 and dies when it works near the fighting: a lost one is not replaced at once, and none is bought when poor
            int now = k.View.Tick;
            if (have > 0) _haulerSeen = now;
            bool lostRecently = have == 0 && now - _haulerSeen < HaulerRebuildTicks;
            bool rich = k.Hud.Cash + k.Hud.Stored >= HaulerCushion;
            bool early = have == 0 && k.Army.Count >= ArmyForFirstHauler;
            if (have < cap && !lostRecently && rich && (early || _wreckValueNow >= WreckValueForHauler)) return _hauler;
        }
        // Dust Sails with the army at the top tiers
        if (_sail >= 0 && _profile is { Air: true } && k.Army.Count >= ArmyBeforeSail && k.Buildable(ProductionCategory.Vehicle, _sail))
        {
            int have = 0;
            foreach (ViewActor a in k.Army) if (a.Kind == ActorKind.Vehicle && a.TypeIndex == _sail) have++;
            foreach (QueueItemView it in q.Items) if (it.TypeIndex == _sail) have++;
            if (have < SailsWanted) return _sail;
        }
        return -1;
    }

    /// <inheritdoc />
    public CellCoord SafeCell(RecruitKnowledge k, CellCoord cell) => cell;

    /// <inheritdoc />
    public CellCoord? ForwardStage(RecruitKnowledge k, CellCoord goal) => null;

    /// <inheritdoc />
    public bool TryEnter(RecruitKnowledge k, ICommandSink sink, ViewActor a, CellCoord stage) => false;

    // ---- early pass: runs before the waves are counted ----------------------------------------------------

    /// <summary>The part that takes units out of the wave pool (raiders) or off the army (packed rigs, salvagers); called once per
    /// decision after the retreat rule and before the military manager.</summary>
    public void Early(RecruitKnowledge k, ICommandSink sink, CellCoord rally, List<ViewActor> knownEnemy, TierPlanner planner)
    {
        Resolve(k.Rules);
        PlayerView view = k.View;
        view.CopyWrecks(_wrecks);
        view.CopyStorms(_storms);
        _wreckValueNow = 0;
        foreach (WreckView w in _wrecks) _wreckValueNow += w.Value;
        UnpackIdleRigs(k, sink);
        if (k.HaveYard) { Salvage(k, sink, knownEnemy); Raid(k, sink, rally, knownEnemy, planner); }
    }

    // ---- unpack -----------------------------------------------------------------------------------------

    /// <summary>Every packed rig that stands idle (and is not part of a relocation) unpacks at a legal site near where it is.</summary>
    private void UnpackIdleRigs(RecruitKnowledge k, ICommandSink sink)
    {
        if (_reloc != Reloc.Idle || k.PackedRigs.Count == 0) return;
        _reserved.Clear();
        foreach (ViewActor r in k.PackedRigs)
        {
            if (!r.Idle || k.View.PackProgressPermille(r.Ref) > 0) continue;
            OrderFailed(r, k.View.Tick, out bool wait0);
            if (wait0) continue;
            if (!FindSite(k, r, r.Cell, 13, out CellCoord site)) continue;
            Reserve(k, r, site);
            NoteOrder(r, k.View.Tick, site);
            Unpacks++;
            sink.Issue(CommandKind.Pack, r.Id, ActorKind.Vehicle, site);
        }
    }

    /// <summary>SUP-5: true when an unpack order given to the rig long enough ago has left it standing idle (the order ended at once);
    /// false while a recent order may still be on its way. Refreshes the order memory.</summary>
    private bool OrderFailed(ViewActor r, int tick, out bool waiting)
    {
        ulong key = RecruitKnowledge.KeyOf(r);
        waiting = false;
        for (int i = 0; i < _ordered.Count; i++)
        {
            if (_ordered[i].Key != key) continue;
            if (tick - _ordered[i].Tick < OrderWaitTicks) { waiting = true; return false; }
            _bad.Add((_ordered[i].X, _ordered[i].Y, tick + 1200));
            _ordered.RemoveAt(i);
            return true;
        }
        return false;
    }

    private void NoteOrder(ViewActor r, int tick, CellCoord site)
    {
        _bad.RemoveAll(b => b.Until < tick);
        ulong key = RecruitKnowledge.KeyOf(r);
        _ordered.RemoveAll(o => o.Key == key || tick - o.Tick > 2400);
        _ordered.Add((key, tick, site.X, site.Y));
    }

    private void Reserve(RecruitKnowledge k, ViewActor rig, CellCoord site)
    {
        int into = k.View.UnpackTypeOf(rig.Id);
        if (into < 0) return;
        BuildingType bt = k.Rules.Buildings[into];
        _reserved.Add((site.X - 1, site.Y - 1, site.X + bt.FootprintW, site.Y + bt.FootprintH));   // a one-cell gap around every rig
    }

    /// <summary>The legal unpack site (top-left) nearest to <paramref name="around"/> that does not touch a site reserved this decision;
    /// wrecks pull the choice toward them (beside a wreck field). Rings of 3, 8 and 13 cells are tried in turn.</summary>
    private bool FindSite(RecruitKnowledge k, ViewActor rig, CellCoord around, int maxRadius, out CellCoord site)
    {
        PlayerView view = k.View;
        site = default;
        int into = view.UnpackTypeOf(rig.Id);
        if (into < 0) return false;
        BuildingType bt = k.Rules.Buildings[into];
        int w = bt.FootprintW, h = bt.FootprintH;
        for (int radius = 3; radius <= maxRadius + 4; radius += 5)
        {
            _cands.Clear();
            for (int y = around.Y - radius - h + 1; y <= around.Y + radius; y++)
                for (int x = around.X - radius - w + 1; x <= around.X + radius; x++)
                {
                    if (x < 1 || y < 1 || x + w > view.MapWidth - 1 || y + h > view.MapHeight - 1) continue;
                    long dx = x * 2L + w - (around.X * 2L + 1), dy = y * 2L + h - (around.Y * 2L + 1);
                    long score = dx * dx + dy * dy - WreckPull(x + w / 2, y + h / 2);
                    _cands.Add((score, x, y));
                }
            _cands.Sort(static (a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            foreach (var (_, x, y) in _cands)
            {
                if (Reserved(x, y, w, h)) continue;
                var tl = new CellCoord(x, y);
                if (!view.CanUnpackAt(rig.Id, tl)) continue;
                site = tl;
                return true;
            }
        }
        return false;
    }

    private bool Reserved(int x, int y, int w, int h)
    {
        foreach (var b in _bad)
            if (Math.Abs(b.X - x) <= 1 && Math.Abs(b.Y - y) <= 1) return true;
        foreach (var r in _reserved)
            if (x <= r.X1 && r.X0 <= x + w - 1 && y <= r.Y1 && r.Y0 <= y + h - 1) return true;
        return false;
    }

    /// <summary>Score bonus (half-cells squared) for a site with wreck value about it: at most about four cells of distance.</summary>
    private long WreckPull(int cx, int cy)
    {
        long sum = 0;
        foreach (WreckView w in _wrecks)
            if (Math.Max(Math.Abs(w.Cell.X - cx), Math.Abs(w.Cell.Y - cy)) <= 10) sum += w.Value;
        return Math.Min(60, sum / 4);
    }

    // ---- salvage ----------------------------------------------------------------------------------------

    private void Salvage(RecruitKnowledge k, ICommandSink sink, List<ViewActor> knownEnemy)
    {
        SimRules rules = k.Rules;
        if (_lastReturn.Count > 0)
        {
            _drop.Clear();
            foreach (ulong key in _lastReturn.Keys)
            {
                bool alive = false;
                foreach (ViewActor s in k.Salvagers) if (RecruitKnowledge.KeyOf(s) == key) { alive = true; break; }
                if (!alive) _drop.Add(key);
            }
            foreach (ulong key in _drop) _lastReturn.Remove(key);
        }
        int[] claims = _wrecks.Count == 0 ? Array.Empty<int>() : new int[_wrecks.Count];
        int tick = k.View.Tick;
        // Haulers first (slot order), then the idle Tinkers of the army
        for (int pass = 0; pass < 2; pass++)
        {
            List<ViewActor> list = pass == 0 ? k.Salvagers : k.Army;
            foreach (ViewActor a in list)
            {
                bool tinker = pass == 1;
                if (tinker && !(a.Kind == ActorKind.Infantry && rules.Infantry[a.TypeIndex] is { Engineer: true, SalvageRate: > 0 })) continue;
                // a Hauler also works the ash fields on its own (mission Harvest): that is when it is taken for a wreck
                if (a.Underground || !(a.Idle || (!tinker && a.Mission == MissionKind.Harvest))) continue;
                int reach = tinker ? TinkerReach : SalvageReach;
                int best = -1;
                long bestScore = long.MinValue;
                for (int i = 0; i < _wrecks.Count; i++)
                {
                    WreckView w = _wrecks[i];
                    int d = Cheb(a.Cell, w.Cell);
                    if (w.Value < 20 || d > reach || w.TicksLeft < d * 14 + 40) continue;
                    if (tinker && Cheb(w.Cell, k.Yard) > reach) continue;
                    if (Dangerous(k, knownEnemy, w.Cell)) continue;
                    long score = (long)w.Value * 1000 / (1 + claims[i]) - d;
                    if (score > bestScore) { bestScore = score; best = i; }   // strict: the lower wreck slot wins a tie
                }
                if (best >= 0)
                {
                    claims[best]++;
                    SalvageOrders++;
                    sink.Issue(CommandKind.Harvest, a.Id, a.Kind, _wrecks[best].Cell);
                }
                else if (!tinker && a.Mission != MissionKind.Harvest)   // session 10: not while it works the ash field (a Return every 400 ticks cost 20 points)
                {   // nothing to salvage: unload what the hopper holds
                    ulong key = RecruitKnowledge.KeyOf(a);
                    if (_lastReturn.TryGetValue(key, out int last) && tick - last < ReturnGapTicks) continue;
                    _lastReturn[key] = tick;
                    sink.Issue(CommandKind.Return, a.Id, a.Kind);
                }
            }
        }
    }

    private bool Dangerous(RecruitKnowledge k, List<ViewActor> knownEnemy, CellCoord cell)
    {
        SimRules rules = k.Rules;
        // session 10: enemy gunmen near the wreck make it unsafe only when our own army there is worth less than they are (a won fight
        // leaves wrecks next to the winners, and that is exactly when the Hauler should go)
        int foe = 0, own = 0;
        foreach (ViewActor e in k.Visible)
            if (e.Relation == Relation.Enemy && Armed(rules, e) && Cheb(e.Cell, cell) <= WreckDangerCells) foe += Cost(rules, e);
        if (foe > 0)
        {
            foreach (ViewActor a in k.Army)
                if (Armed(rules, a) && Cheb(a.Cell, cell) <= WreckDangerCells) own += Cost(rules, a);
            if (own < foe) return true;
        }
        foreach (ViewActor b in knownEnemy)
            if (rules.Buildings[b.TypeIndex].PrimaryWeapon >= 0 && Cheb(b.Cell, cell) <= WreckDangerCells + 2) return true;
        return false;
    }

    private static bool Armed(SimRules rules, ViewActor a) => a.Kind switch
    {
        ActorKind.Vehicle => rules.Vehicles[a.TypeIndex].PrimaryWeapon >= 0 && rules.Vehicles[a.TypeIndex].HarvestCapacity == 0,
        ActorKind.Infantry => rules.Infantry[a.TypeIndex].PrimaryWeapon >= 0,
        _ => false,
    };

    // ---- raids ------------------------------------------------------------------------------------------

    private int PartyCap => Tier >= 4 ? PartyTop : Tier == 3 ? PartyMid : PartyLow;

    private static bool IsRaider(SimRules rules, ViewActor a) => a.Kind switch
    {
        ActorKind.Vehicle => rules.Vehicles[a.TypeIndex].Raider,
        ActorKind.Infantry => rules.Infantry[a.TypeIndex].Raider,
        _ => false,
    };

    private void Raid(RecruitKnowledge k, ICommandSink sink, CellCoord rally, List<ViewActor> knownEnemy, TierPlanner planner)
    {
        if (_squads is null) return;
        SimRules rules = k.Rules;
        int tick = k.View.Tick;
        // recruit: idle raiders of the army up to the cap, in army order
        if (_squads.RaiderCount < PartyCap)
        {
            for (int i = 0; i < k.Army.Count && _squads.RaiderCount < PartyCap; i++)
            {
                ViewActor a = k.Army[i];
                if (!a.Idle || a.Underground || !IsRaider(rules, a)) continue;
                _squads.AssignRaider(a);
                _squads.Detach(k, a);
                i--;
            }
        }
        if (_squads.RaiderCount == 0) return;
        // hurt raiders go home for good
        for (int i = k.Detached.Count - 1; i >= 0; i--)
        {
            ViewActor a = k.Detached[i];
            if (!_squads.IsRaider(a) || a.MaxHealth <= 0 || a.Health * 100 >= a.MaxHealth * RaiderHurtPct) continue;
            _squads.SendHome(k, a);
            sink.Issue(CommandKind.Move, a.Id, a.Kind, rally);
        }
        int ready = 0;
        foreach (ViewActor a in k.Detached) if (_squads.IsRaider(a) && a.Idle && !a.Underground) ready++;
        if (ready == 0) return;

        ViewActor? target = null;
        CellCoord aim = default;
        bool haveAim = false;
        CellCoord from = k.Yard;
        if (tick < _chainUntil) from = _chain;   // chained onto the storm
        long bestD = long.MaxValue;
        foreach (ViewActor b in knownEnemy)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (!(bt.Refinery || bt.Storage > 0)) continue;
            long d = Dist2(b.Cell, from);
            if (d >= bestD) continue;
            bestD = d; target = b; aim = b.Cell; haveAim = true;
        }
        if (!haveAim && planner.TryHarvesterTarget(k, _profile?.ScoutInfoAgeTicks ?? 600, out CellCoord hcell, out _, out _)) { aim = hcell; haveAim = true; }
        if (!haveAim)
        {
            bestD = long.MaxValue;
            foreach (ViewActor b in knownEnemy)
            {
                long d = Dist2(b.Cell, from);
                if (d >= bestD) continue;
                bestD = d; aim = b.Cell; haveAim = true;
            }
        }
        if (!haveAim)
        {   // probe the likely enemy starts, moving on when a raider stands there
            aim = _probes[_probeIdx % _probes.Count];
            foreach (ViewActor a in k.Detached)
                if (_squads.IsRaider(a) && Cheb(a.Cell, aim) <= 8) { _probeIdx = (_probeIdx + 1) % _probes.Count; aim = _probes[_probeIdx]; break; }
            haveAim = true;
        }
        // set out together: the party leaves when enough raiders wait, or at once when it is the whole squad
        bool party = ready >= PartyStart || ready >= _squads.RaiderCount;
        if (!party) return;
        bool sent = false;
        foreach (ViewActor a in k.Detached)
        {
            if (!_squads.IsRaider(a) || !a.Idle || a.Underground) continue;
            if (target is ViewActor t && !t.Ghost)
            {
                RaidOrders++; sent = true;
                sink.Issue(CommandKind.Enter, a.Id, a.Kind, default, new ActorRef(ActorKind.Building, t.Id));
            }
            else if (Cheb(a.Cell, aim) > 2)
            {
                RaidOrders++; sent = true;
                sink.Issue(CommandKind.AttackMove, a.Id, a.Kind, aim);
            }
        }
        if (sent) RaidParties++;
    }

    // ---- relocation -------------------------------------------------------------------------------------

    private void Relocate(RecruitKnowledge k, ICommandSink sink)
    {
        PlayerView view = k.View;
        int tick = view.Tick;
        SimRules rules = k.Rules;
        if (_reloc == Reloc.Idle)
        {
            if (!k.HaveYard || k.PackedRigs.Count > 0 || tick < _relocCooldownUntil) { _threatStreak = 0; return; }
            if (LastStand(k)) { _threatStreak = 0; return; }
            int enemy = EnemyNearBase(k, out CellCoord enemyCentre);
            int own = 0;
            foreach (ViewActor a in k.Army) if (Cheb(a.Cell, k.Yard) <= ThreatCells + 4) own += Cost(rules, a);
            foreach (ViewActor a in k.Home) if (Cheb(a.Cell, k.Yard) <= ThreatCells + 4) own += Cost(rules, a);
            Debug = $"enemy {enemy} own {own} streak {_threatStreak} laststand {LastStand(k)} cooldown {_relocCooldownUntil}";
            bool heavy = enemy >= HeavyThreat && (long)enemy * 100 >= (long)own * ThreatOverArmyPct;
            _threatStreak = heavy ? _threatStreak + 1 : 0;
            if (_threatStreak < ThreatStreakNeeded) return;
            // any packable rig to take along?
            int packable = 0;
            foreach (ViewActor b in k.Buildings) if (rules.Buildings[b.TypeIndex].PacksInto >= 0) packable++;
            if (packable == 0) return;
            _dest = ChooseFallback(k, enemyCentre);
            if (Cheb(_dest, k.Yard) < RelocateMinCells) return;
            _oldAnchor = k.Yard;
            _reloc = Reloc.Moving; _relocStart = tick; _seenPacked = false; _threatStreak = 0;
            Relocations++;
            // pack the Mother Rig first, then the other rigs in slot order
            foreach (ViewActor b in k.Buildings)
                if (rules.Buildings[b.TypeIndex].PacksInto >= 0 && b.TypeIndex == _mother) sink.Issue(CommandKind.Pack, b.Id, ActorKind.Building);
            bool capNear = BuildOrderManager.SupplyShort(k, BuildOrderManager.MarginForTier(Tier));   // SUP-5: a still is not packed while the cap is near
            foreach (ViewActor b in k.Buildings)
                if (rules.Buildings[b.TypeIndex].PacksInto >= 0 && b.TypeIndex != _mother && !(capNear && rules.Buildings[b.TypeIndex].SupplyProvided > 0))
                    sink.Issue(CommandKind.Pack, b.Id, ActorKind.Building);
            return;
        }

        // under way: drive each packed rig to a free site beside the fallback; done when nothing is packed or packing any more
        if (k.PackedRigs.Count > 0) _seenPacked = true;
        _reserved.Clear();
        foreach (ViewActor r in k.PackedRigs)
        {
            if (!r.Idle || view.PackProgressPermille(r.Ref) > 0) continue;
            // no room beside the fallback (or it is taking too long, or the last order ended at once): unpack where the rig stands
            bool failed = OrderFailed(r, tick, out bool waiting);
            if (waiting) continue;
            CellCoord site = default;
            bool found = !failed && tick - _relocStart < 1200 && FindSite(k, r, _dest, 13, out site);
            if (!found && !FindSite(k, r, r.Cell, 13, out site)) continue;
            Reserve(k, r, site);
            NoteOrder(r, tick, site);
            sink.Issue(CommandKind.Pack, r.Id, ActorKind.Vehicle, site);
        }
        bool packing = false;
        foreach (ViewActor b in k.Buildings)
            if (rules.Buildings[b.TypeIndex].PacksInto >= 0 && view.PackProgressPermille(b.Ref) > 0) packing = true;
        if (_seenPacked && !packing && k.PackedRigs.Count == 0 && tick - _relocStart >= 60)
        {
            _reloc = Reloc.Idle; _relocCooldownUntil = tick + RelocateCooldownTicks;
            RelocationsDone++;
            SellLeftBehind(k, sink);
        }
        else if (tick - _relocStart >= RelocateTimeoutTicks)
        {
            _reloc = Reloc.Idle; _relocCooldownUntil = tick + RelocateCooldownTicks;
            RelocationsAborted++;
        }
    }

    /// <summary>The last stand: no income and no money for a Gleaner, or no Garage to build one in. A side in it stays and fights.</summary>
    private bool LastStand(RecruitKnowledge k)
    {
        SimRules rules = k.Rules;
        if (_gleaner < 0) return true;
        int funds = k.Hud.Cash + k.Hud.Stored;
        return k.Harvesters.Count == 0 && (funds < rules.Vehicles[_gleaner].Cost || !k.Buildable(ProductionCategory.Vehicle, _gleaner));
    }

    /// <summary>Credit value of the enemy gunmen in view within <see cref="ThreatCells"/> of the base; their centre.</summary>
    private int EnemyNearBase(RecruitKnowledge k, out CellCoord centre)
    {
        SimRules rules = k.Rules;
        int sum = 0, n = 0;
        long sx = 0, sy = 0;
        foreach (ViewActor e in k.Visible)
        {
            if (e.Relation != Relation.Enemy || !e.IsMobile || !Armed(rules, e) || Cheb(e.Cell, k.Yard) > ThreatCells) continue;
            sum += Cost(rules, e); n++; sx += e.Cell.X; sy += e.Cell.Y;
        }
        centre = n == 0 ? k.Yard : new CellCoord((int)(sx / n), (int)(sy / n));
        return sum;
    }

    private static int Cost(SimRules rules, ViewActor a) =>
        a.Kind == ActorKind.Vehicle ? rules.Vehicles[a.TypeIndex].Cost : a.Kind == ActorKind.Infantry ? rules.Infantry[a.TypeIndex].Cost : 0;

    /// <summary>The place to rebuild: the richest wreck field or own storm that is far enough from the base and farther from the enemy than
    /// the base is; else a cell <see cref="RelocateAwayCells"/> from the base straight away from the threat.</summary>
    private CellCoord ChooseFallback(RecruitKnowledge k, CellCoord enemyCentre)
    {
        PlayerView view = k.View;
        long baseToEnemy = Dist2(k.Yard, enemyCentre);
        CellCoord best = default;
        long bestScore = long.MinValue;
        bool found = false;
        void Consider(CellCoord c, long value)
        {
            if (!view.InMap(c) || Cheb(c, k.Yard) < RelocateMinCells || Cheb(c, k.Yard) > 44) return;
            if (Dist2(c, enemyCentre) <= baseToEnemy + 64) return;   // not clearly away from the threat
            if (!view.IsPassable(c, Locomotion.Tracked)) return;
            long score = value * 100 - Cheb(c, k.Yard) * 10;
            if (score > bestScore) { bestScore = score; best = c; found = true; }
        }
        foreach (WreckView w in _wrecks)
        {
            if (w.Value < 100) continue;
            long field = 0;
            foreach (WreckView o in _wrecks) if (Cheb(o.Cell, w.Cell) <= 6) field += o.Value;
            Consider(w.Cell, field);
        }
        foreach (StormView s in _storms)
            if (s.Owner == view.Player && s.TicksLeft > 200)
                Consider(new CellCoord(s.Centre.X, s.Centre.Y + s.Radius + 2), 400);   // beside the drift, not under it
        if (found) return best;
        // no wreck field or drift: the passable cell, eight directions at three distances, that lies farthest from the threat
        CellCoord pick = k.Yard;
        long pickD = -1;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                for (int d = RelocateAwayCells; d >= RelocateMinCells; d -= 4)
                {
                    var c = new CellCoord(Math.Clamp(k.Yard.X + dx * d, 3, view.MapWidth - 4), Math.Clamp(k.Yard.Y + dy * d, 3, view.MapHeight - 4));
                    if (Cheb(c, k.Yard) < RelocateMinCells || !view.IsPassable(c, Locomotion.Tracked)) continue;
                    long dist = Dist2(c, enemyCentre);
                    if (dist > pickD) { pickD = dist; pick = c; }
                }
            }
        return pick;
    }

    /// <summary>After the move: the fixed structures left at the old site are sold (the build order rebuilds them beside the new rig).</summary>
    private void SellLeftBehind(RecruitKnowledge k, ICommandSink sink)
    {
        SimRules rules = k.Rules;
        if (!k.HaveYard) return;
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (bt.PacksInto >= 0 || bt.Wall || bt.Gate || bt.Cost <= 0 || Cheb(b.Cell, k.Yard) <= SellBeyondCells) continue;
            Sold++;
            sink.Issue(CommandKind.Sell, b.Id, ActorKind.Building);
        }
    }

    // ---- The Second Drift -------------------------------------------------------------------------------

    private void FireStorm(RecruitKnowledge k, ICommandSink sink)
    {
        if (k.Hud.Superweapons.Count == 0) return;
        int tick = k.View.Tick;
        bool anyReady = false;
        foreach (SuperweaponView sw in k.Hud.Superweapons) if (sw.Ready && !sw.Paused) anyReady = true;
        if (!anyReady || tick - _lastFire < FireGapTicks) return;
        if (!PickStormTarget(k, out CellCoord target)) return;
        foreach (SuperweaponView sw in k.Hud.Superweapons)
        {
            if (!sw.Ready || sw.Paused) continue;
            _lastFire = tick; StormOrders++;
            _chain = target; _chainUntil = tick + ChainTicks;
            sink.Issue(CommandKind.Special, sw.Building, ActorKind.Building, target);
            return;
        }
    }

    /// <summary>The enemy's biggest army cluster in view (worth at least <see cref="ArmyClusterValue"/>), else the densest known enemy base cluster.</summary>
    private bool PickStormTarget(RecruitKnowledge k, out CellCoord target)
    {
        SimRules rules = k.Rules;
        target = default;
        int bestValue = ArmyClusterValue - 1;
        bool found = false;
        for (int i = 0; i < k.Visible.Count; i++)
        {
            ViewActor u = k.Visible[i];
            if (u.Relation != Relation.Enemy || !u.IsMobile || !Armed(rules, u)) continue;
            int v = 0;
            foreach (ViewActor o in k.Visible)
                if (o.Relation == Relation.Enemy && o.IsMobile && Armed(rules, o) && Cheb(o.Cell, u.Cell) <= ClusterCells) v += Cost(rules, o);
            if (v > bestValue) { bestValue = v; target = u.Cell; found = true; }
        }
        if (found) return true;
        var known = new List<ViewActor>();
        k.View.CopyKnownEnemyBuildings(known);
        int bestCount = BaseClusterMin - 1;
        foreach (ViewActor t in known)
        {
            int n = 0;
            foreach (ViewActor o in known) if (Cheb(o.Cell, t.Cell) <= ClusterCells) n++;
            if (n > bestCount) { bestCount = n; target = t.Cell; found = true; }
        }
        return found;
    }

    // ---- helpers ----------------------------------------------------------------------------------------

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    private static long Dist2(CellCoord a, CellCoord b)
    {
        long dx = a.X - b.X, dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }

    // ---- save / restore (private format, appended by RecruitAi for a Left Behind side only) ----------------------

    /// <summary>Writes the relocation, chain and probe state, the Return timers and the counters.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write((byte)_reloc); w.Write(_relocStart); w.Write(_relocCooldownUntil);
        AiSnapshot.WriteCell(w, _dest); AiSnapshot.WriteCell(w, _oldAnchor);
        w.Write(_seenPacked); w.Write(_threatStreak);
        w.Write(_lastFire); AiSnapshot.WriteCell(w, _chain); w.Write(_chainUntil);
        w.Write(_probeIdx);
        AiSnapshot.WriteMap(w, _lastReturn, (bw, v) => bw.Write(v));
        w.Write(Unpacks); w.Write(RaidParties); w.Write(RaidOrders); w.Write(SalvageOrders);
        w.Write(Relocations); w.Write(RelocationsDone); w.Write(RelocationsAborted); w.Write(Sold); w.Write(StormOrders);
        w.Write(_wreckValueNow); w.Write(_haulerSeen);
        w.Write(_ordered.Count);
        foreach (var o in _ordered) { w.Write(o.Key); w.Write(o.Tick); w.Write(o.X); w.Write(o.Y); }
        w.Write(_bad.Count);
        foreach (var b in _bad) { w.Write(b.X); w.Write(b.Y); w.Write(b.Until); }
    }

    /// <summary>Reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        _reloc = (Reloc)r.ReadByte(); _relocStart = r.ReadInt32(); _relocCooldownUntil = r.ReadInt32();
        _dest = AiSnapshot.ReadCell(r); _oldAnchor = AiSnapshot.ReadCell(r);
        _seenPacked = r.ReadBoolean(); _threatStreak = r.ReadInt32();
        _lastFire = r.ReadInt32(); _chain = AiSnapshot.ReadCell(r); _chainUntil = r.ReadInt32();
        _probeIdx = r.ReadInt32();
        AiSnapshot.ReadMap(r, _lastReturn, br => br.ReadInt32());
        Unpacks = r.ReadInt32(); RaidParties = r.ReadInt32(); RaidOrders = r.ReadInt32(); SalvageOrders = r.ReadInt32();
        Relocations = r.ReadInt32(); RelocationsDone = r.ReadInt32(); RelocationsAborted = r.ReadInt32(); Sold = r.ReadInt32(); StormOrders = r.ReadInt32();
        _wreckValueNow = r.ReadInt32(); _haulerSeen = r.ReadInt32();
        int n = AiSnapshot.Count(r);
        _ordered.Clear();
        for (int i = 0; i < n; i++) _ordered.Add((r.ReadUInt64(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
        n = AiSnapshot.Count(r);
        _bad.Clear();
        for (int i = 0; i < n; i++) _bad.Add((r.ReadInt32(), r.ReadInt32(), r.ReadInt32()));
    }
}
