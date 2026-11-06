// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// M7-10: what the Expedition AI does beyond the shared loop (design 07 §7). Fog-honest: it reads only its <see cref="PlayerView"/>
/// (terrain and sea regions are public, tenders are its own) and the order history it keeps itself.
/// <list type="bullet">
/// <item>Establish: the base starts as a Command Ship at sea. It sails to the landing coast nearest to where it is (a coast where the
/// Anchorage's footprint also fits is preferred: the dock needs water on its south side) and Deploys into the Landing Depot. The
/// Beacons of the build order go to the frontier cell nearest to the enemy (the zone only grows by a beacon at a time, build radius 2).</item>
/// <item>Convoy: while a shipment is due soon or a tender sails, 1 escort (2 at profile tier 3+) stands at the dock; a hurt or threatened tender
/// sends the escorts to it.</item>
/// <item>Strike: ships are not army (a wave cannot sail over land). The ships that are neither escort, hurt nor home reserve go out in a group
/// of at least <see cref="StrikeMin"/> to the sea cell nearest the nearest reachable enemy building that is still inside the group's gun
/// range, and fire on it. A ship below <see cref="RetreatPct"/> percent health goes home to a Mender and returns healed. Profile tier 2+: sub
/// hunting (Launches, bought when an enemy submarine was seen), focus fire (flag), carrier sorties (Attack on the carrier), the Salvo Array
/// through the shared superweapon manager.</item>
/// <item>Withdraw: when the army value falls well below the enemy value seen lately, waves stop and every ship goes home until it recovers.</item>
/// </list>
/// </summary>
internal sealed class ExpeditionTactics : IFactionTactics
{
    // ---- thresholds: every (tune) number of the Expedition AI lives here ----

    /// <summary>Extra units every wave waits for (few, dear units).</summary>
    public const int WaveBonus = 2;   // (tune) M7-10
    /// <summary>Soldiers kept alive at once; the infantry lane stops there and the money goes to hulls.</summary>
    public const int InfantryCap = 6;   // (tune) M7-10
    /// <summary>A unit below this health percent leaves the fight and repairs (design 07 §7).</summary>
    public const int RetreatPct = 40;   // (tune) M7-10
    /// <summary>A repaired ship rejoins at this health percent.</summary>
    public const int RepairDonePct = 90;   // (tune) M7-10
    /// <summary>Smallest ship group that goes on a strike.</summary>
    public const int StrikeMin = 1;   // (tune) M7-10
    /// <summary>Ships kept at home out of strikes: none below 3 ships, 1 from 3, 2 from 5.</summary>
    public const int ReserveFrom3 = 1, ReserveFrom5 = 2, ReserveMin = 4;   // (tune) M7-10
    /// <summary>Fleet size wanted (ships alive and queued) by profile tier (1-2, 3, 4-5).</summary>
    public const int FleetLow = 3, FleetMid = 4, FleetTop = 5;   // (tune) M7-10
    /// <summary>Own land and air units that must stand before the first ship is bought.</summary>
    public const int ArmyBeforeShips = 2;   // (tune) M7-10
    /// <summary>Own units (all kinds) before a Mender is bought.</summary>
    public const int ArmyBeforeMender = 4;   // (tune) M7-10
    /// <summary>Armed land vehicles that must stand before the Quay, Shipyard and Flight Deck are started (the money goes to Glide Tanks first).</summary>
    public const int TanksBeforeNavy = 1;   // (tune) M7-10

    /// <summary>Ticks to the next shipment at which the escorts take station.</summary>
    public const int EscortEtaTicks = 1500;   // (tune) M7-10
    /// <summary>Ticks the escorts stay on station after the last tender arrived or the dock lost its timer.</summary>
    public const int EscortLingerTicks = 600;   // (tune) M7-10
    /// <summary>Cells from a tender within which a visible enemy counts as a threat to it.</summary>
    public const int TenderDangerCells = 10;   // (tune) M7-10
    /// <summary>Cells from the dock at which an escort counts as on station.</summary>
    public const int StationCells = 3;   // (tune) M7-10

    /// <summary>Withdraw: the enemy value seen lately must be at least this many credits.</summary>
    public const int WithdrawMinEnemy = 1500;   // (tune) M7-10
    /// <summary>Withdraw starts when own army value is below this percent of the enemy value, and ends at <see cref="WithdrawResumePct"/>.</summary>
    public const int WithdrawPct = 60, WithdrawResumePct = 90;   // (tune) M7-10
    /// <summary>Longest a withdraw lasts, and the pause before the next one.</summary>
    public const int WithdrawMaxTicks = 3000, WithdrawCooldownTicks = 1800;   // (tune) M7-10

    /// <summary>Cells scanned for a landing site around the Command Ship, and how many nearest sites are tried for an Anchorage fit.</summary>
    public const int SiteRadius = 40, SiteFitScan = 600;   // (tune) M7-10
    /// <summary>Cells from the depot within which an Anchorage footprint must fit for a site to be preferred.</summary>
    public const int AnchorageReach = 8;   // (tune) M7-10 (inside the depot's beachhead radius of 10 with room for the footprint)
    /// <summary>Cells from the depot within which a map landing marker makes a site preferred.</summary>
    public const int MarkerCells = 8;   // (tune) M7-10
    /// <summary>Ticks to wait for a Deploy, or for the ship to reach its site, before the site is given up.</summary>
    public const int DeployWaitTicks = 180, SiteWaitTicks = 2400;   // (tune) M7-10
    /// <summary>Ticks between Move orders to an idle ship that is not yet where it should be.</summary>
    public const int MoveGapTicks = 120;   // (tune) M7-10

    /// <summary>Cells scanned around a target for a firing position (gun range is the other limit), percent of the range used as stand-off.</summary>
    public const int ScanCells = 14, StandOffPct = 90;   // (tune) M7-10
    /// <summary>Ticks a strike target is kept before it is chosen again.</summary>
    public const int StrikeReviewTicks = 1200;   // (tune) M7-10
    /// <summary>Known enemy buildings looked at when a strike target is chosen (nearest first).</summary>
    public const int TargetsLooked = 10;   // (tune) M7-10
    /// <summary>Cells from the group within which visible enemies get focus fire.</summary>
    public const int FocusCells = 9;   // (tune) M7-10

    /// <summary>Carrier: enemies within this many cells of the carrier are worth a sortie; ticks between sorties of one carrier.</summary>
    public const int SortieCells = 14, SortieGapTicks = 600;   // (tune) M7-10
    /// <summary>Launches wanted when an enemy submarine was seen.</summary>
    public const int SubHuntersWanted = 2;   // (tune) M7-10
    /// <summary>Sparkers wanted once enemy aircraft were seen.</summary>
    public const int SparkersWanted = 2;   // (tune) M7-10

    private readonly DifficultyProfile? _profile;
    private Squads? _squads;
    private MilitaryManager? _military;
    private TierPlanner? _planner;
    private int _mapW, _mapH;
    private CellCoord _home, _front;

    // resolved ids
    private bool _resolved;
    private int _depot = -1, _anchorage = -1, _beacon = -1, _commandShip = -1, _mender = -1, _destroyer = -1, _cruiser = -1, _carrier = -1,
        _launch = -1, _sparker = -1, _tender = -1;

    // establish
    private int _homeRegion;
    private bool _siteSet;
    private CellCoord _siteCell;
    private int _siteSince, _deployTick = -100000, _moveTick = -100000, _deployTries;
    private readonly List<CellCoord> _bad = new();

    // convoy / fleet memory (keys only, never sim state)
    private readonly HashSet<ulong> _escort = new(), _hurt = new();
    private int _lastConvoyTick = -100000;
    private bool _strikeSet, _probe;
    private CellCoord _strikeCell, _strikeBuilding;
    private int _strikeSince;
    private readonly Dictionary<ulong, int> _lastSortie = new(), _deckRally = new();
    private readonly Dictionary<ulong, CellCoord> _deckCell = new();

    // withdraw
    private bool _withdraw;
    private int _withdrawSince, _withdrawEnd = -100000;
    private bool _enemySubSeen;

    // scratch lists (cleared before use)
    private readonly List<ViewActor> _pool = new(), _group = new(), _free = new();
    private readonly List<TenderView> _tenders = new();
    private readonly List<(long Score, int X, int Y, int Tx, int Ty)> _cands = new();
    private readonly List<ulong> _dead = new();

    /// <summary>Creates the tactics for a tier's profile (null: the lowest tier).</summary>
    public ExpeditionTactics(DifficultyProfile? profile) => _profile = profile;

    private int Tier => _profile?.Tier ?? 1;

    /// <summary>Diagnostics: the last site / state line.</summary>
    public string Debug { get; private set; } = "";
    /// <summary>Deploy orders given to the Command Ship.</summary>
    public int Deploys { get; private set; }
    /// <summary>Move orders given to the Command Ship.</summary>
    public int SailOrders { get; private set; }
    /// <summary>Ship orders skipped because the goal was not in the ship's sea.</summary>
    public int Dropped { get; private set; }
    /// <summary>Strike groups sent (a new target chosen for a group).</summary>
    public int Strikes { get; private set; }
    /// <summary>Orders that sent escorts to a tender or the dock.</summary>
    public int EscortOrders { get; private set; }
    /// <summary>Times a tender was found hurt or threatened.</summary>
    public int ConvoyAlerts { get; private set; }
    /// <summary>Ships sent home for low health.</summary>
    public int ShipRetreats { get; private set; }
    /// <summary>Withdraws started.</summary>
    public int Withdrawals { get; private set; }
    /// <summary>Carrier sorties ordered.</summary>
    public int Sorties { get; private set; }
    /// <summary>Attack orders on an enemy submarine.</summary>
    public int SubHunts { get; private set; }
    /// <summary>Beacons placed at the frontier.</summary>
    public int BeaconsPlaced { get; private set; }
    /// <summary>True once the depot stands.</summary>
    public bool Established { get; private set; }
    /// <summary>True while the AI withdraws.</summary>
    public bool Withdrawing => _withdraw;

    /// <inheritdoc />
    public void Start(AiContext ctx)
    {
        _mapW = ctx.MapWidth; _mapH = ctx.MapHeight; _home = ctx.StartCell;
        _front = new CellCoord(_mapW / 2, _mapH / 2);
        long best = long.MaxValue;
        foreach (CellCoord s in ctx.StartPositions)
        {
            if (Cheb(s, ctx.StartCell) <= 6) continue;
            long d = Dist2(s, ctx.StartCell);
            if (d < best) { best = d; _front = s; }
        }
    }

    /// <summary>Hands over the shared managers the Expedition steers (squads, the wave manager).</summary>
    public void Attach(Squads squads, MilitaryManager military) { _squads = squads; _military = military; }

    private void Resolve(SimRules rules)
    {
        if (_resolved) return;
        _resolved = true;
        _depot = SimRules.IndexOf(rules.Buildings, "exp_landing_depot", b => b.Id);
        _anchorage = SimRules.IndexOf(rules.Buildings, "exp_anchorage", b => b.Id);
        _beacon = SimRules.IndexOf(rules.Buildings, "exp_beacon", b => b.Id);
        _commandShip = SimRules.IndexOf(rules.Vehicles, "exp_command_ship", v => v.Id);
        _mender = SimRules.IndexOf(rules.Vehicles, "exp_repair_hover", v => v.Id);
        _destroyer = SimRules.IndexOf(rules.Vehicles, "exp_destroyer", v => v.Id);
        _cruiser = SimRules.IndexOf(rules.Vehicles, "exp_cruiser", v => v.Id);
        _carrier = SimRules.IndexOf(rules.Vehicles, "exp_carrier", v => v.Id);
        _launch = SimRules.IndexOf(rules.Vehicles, "exp_launch", v => v.Id);
        _sparker = SimRules.IndexOf(rules.Vehicles, "exp_sparker", v => v.Id);
        _tender = SimRules.IndexOf(rules.Vehicles, rules.Shipments.TenderType, v => v.Id);
    }

    // ---- IFactionTactics --------------------------------------------------------------------------------

    /// <inheritdoc />
    public void Think(RecruitKnowledge k, ICommandSink sink, CellCoord goal, CellCoord rally, LeptonCoord? waveCentre)
    {
        Resolve(k.Rules);
        if (!k.HaveYard) return;
        Carriers(k, sink);
    }

    /// <inheritdoc />
    public CellCoord SafeCell(RecruitKnowledge k, CellCoord cell) => cell;

    /// <inheritdoc />
    public CellCoord? ForwardStage(RecruitKnowledge k, CellCoord goal) => null;

    /// <inheritdoc />
    public bool TryEnter(RecruitKnowledge k, ICommandSink sink, ViewActor a, CellCoord stage) => false;

    /// <inheritdoc />
    public bool HoldInfantry(RecruitKnowledge k)
    {
        int n = 0;
        foreach (ViewActor a in k.Own) if (a.Kind == ActorKind.Infantry) n++;
        return n >= InfantryCap;
    }

    /// <inheritdoc />
    public CellCoord? RallyFor(RecruitKnowledge k, ViewActor factory)
    {
        BuildingType bt = k.Rules.Buildings[factory.TypeIndex];
        if (!bt.WaterBuilding || bt.Produces == null) return null;
        CellCoord? w = NearestSea(k.View, factory.Cell, _homeRegion, 8, false);
        return w;
    }

    /// <inheritdoc />
    public int WantedVehicle(RecruitKnowledge k)
    {
        Resolve(k.Rules);
        if (!k.HaveYard) return -1;
        SimRules rules = k.Rules;
        QueueView q = k.Queue(ProductionCategory.Vehicle);
        int ground = k.Army.Count;
        // a Launch or two against enemy submarines (only Launches carry depth charges)
        if (_enemySubSeen && _launch >= 0 && k.Buildable(ProductionCategory.Vehicle, _launch) && Count(k, q, _launch) < SubHuntersWanted) return _launch;
        // Sparkers once enemy aircraft were seen
        if (_sparker >= 0 && _planner is { SeenEnemyAir: true } && k.Buildable(ProductionCategory.Vehicle, _sparker) && Count(k, q, _sparker) < SparkersWanted) return _sparker;
        // one Mender to repair the dear units
        if (_mender >= 0 && k.Buildable(ProductionCategory.Vehicle, _mender) && ArmedVehicles(k) >= TanksBeforeNavy && ground + k.Fleet.Count >= ArmyBeforeMender && Count(k, q, _mender) < 1)
            return _mender;
        // the fleet: destroyers first, a cruiser and a carrier at the higher tiers
        if (ground >= ArmyBeforeShips && k.Harvesters.Count > 0)
        {
            int fleet = k.Fleet.Count;
            foreach (QueueItemView it in q.Items) if (rules.Vehicles[it.TypeIndex].Locomotion == Locomotion.Naval) fleet++;
            int want = Tier >= 4 ? FleetTop : Tier >= 3 ? FleetMid : FleetLow;
            if (fleet < want)
            {
                if (Tier >= 4 && _carrier >= 0 && k.Buildable(ProductionCategory.Vehicle, _carrier) && Count(k, q, _carrier) < 1) return _carrier;
                if (Tier >= 3 && _cruiser >= 0 && k.Buildable(ProductionCategory.Vehicle, _cruiser) && Count(k, q, _cruiser) < 1 + (Tier >= 5 ? 1 : 0)) return _cruiser;
                if (_destroyer >= 0 && k.Buildable(ProductionCategory.Vehicle, _destroyer)) return _destroyer;
            }
        }
        return -1;
    }

    private static int ArmedVehicles(RecruitKnowledge k)
    {
        int n = 0;
        foreach (ViewActor a in k.Army) if (a.Kind == ActorKind.Vehicle) n++;
        foreach (ViewActor a in k.Home) if (a.Kind == ActorKind.Vehicle) n++;
        return n;
    }

    private static int Count(RecruitKnowledge k, QueueView q, int type)
    {
        int n = 0;
        foreach (QueueItemView it in q.Items) if (it.TypeIndex == type) n++;
        foreach (ViewActor a in k.Own) if (a.Kind == ActorKind.Vehicle && a.TypeIndex == type) n++;
        return n;
    }

    // ---- build hooks ------------------------------------------------------------------------------------

    /// <summary>True when the water structure <paramref name="id"/> has no legal cell near the depot (the build order skips it for now, so the
    /// structure lane is never blocked by a ready structure that cannot be placed). Other structures: false.</summary>
    public bool NoSite(RecruitKnowledge k, string id)
    {
        if (!k.HaveYard) return false;
        // the navy waits for an army: Glide Tanks first (the Plotting Room opens them), then the yards
        if (id is "exp_quay" or "exp_shipyard" or "exp_flight_deck" && ArmedVehicles(k) < TanksBeforeNavy) return true;
        int t = SimRules.IndexOf(k.Rules.Buildings, id, b => b.Id);
        if (t < 0 || !k.Rules.Buildings[t].WaterBuilding) return false;
        PlayerView v = k.View;
        for (int y = k.Yard.Y - 12; y <= k.Yard.Y + 14; y++)
            for (int x = k.Yard.X - 12; x <= k.Yard.X + 14; x++)
                if (v.CanPlace(t, new CellCoord(x, y))) return false;
        return true;
    }

    /// <summary>The cell of a ready Beacon: the legal cell nearest to the enemy start among those around the base, with a one-cell gap to other
    /// buildings when one exists. Anything else: not chosen here.</summary>
    public (bool Ok, CellCoord Cell) PlaceBeacon(RecruitKnowledge k, int type)
    {
        Resolve(k.Rules);
        if (type != _beacon || type < 0 || k.Buildings.Count == 0) return (false, default);
        PlayerView v = k.View;
        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            x0 = Math.Min(x0, b.Cell.X); y0 = Math.Min(y0, b.Cell.Y);
            x1 = Math.Max(x1, b.Cell.X + bt.FootprintW - 1); y1 = Math.Max(y1, b.Cell.Y + bt.FootprintH - 1);
        }
        long best = long.MaxValue, bestGap = long.MaxValue;
        CellCoord pick = default, pickGap = default;
        for (int y = y0 - 3; y <= y1 + 3; y++)
            for (int x = x0 - 3; x <= x1 + 3; x++)
            {
                var c = new CellCoord(x, y);
                if (!v.CanPlace(type, c)) continue;
                long d = Dist2(c, _front);
                if (d < best) { best = d; pick = c; }
                if (d < bestGap && GapOk(k, c)) { bestGap = d; pickGap = c; }
            }
        if (best == long.MaxValue) return (false, default);
        BeaconsPlaced++;
        return (true, bestGap != long.MaxValue ? pickGap : pick);
    }

    private static bool GapOk(RecruitKnowledge k, CellCoord c)
    {
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            int dx = Math.Max(0, Math.Max(b.Cell.X - c.X, c.X - (b.Cell.X + bt.FootprintW - 1)));
            int dy = Math.Max(0, Math.Max(b.Cell.Y - c.Y, c.Y - (b.Cell.Y + bt.FootprintH - 1)));
            if (Math.Max(dx, dy) <= 1) return false;
        }
        return true;
    }

    // ---- early pass: runs before the waves are counted --------------------------------------------------

    /// <summary>Establish, convoy and fleet; called once per decision after the retreat rule and before the military manager.</summary>
    public void Early(RecruitKnowledge k, ICommandSink sink, CellCoord rally, List<ViewActor> knownEnemy, TierPlanner planner)
    {
        Resolve(k.Rules);
        _planner = planner;
        if (!k.HaveYard) { Established = false; Establish(k, sink); return; }
        Established = true;
        PlayerView v = k.View;
        v.CopyOwnTenders(_tenders);
        foreach (ViewActor e in k.Visible) if (e.Relation == Relation.Enemy && e.Submerged) _enemySubSeen = true;
        EvaluateWithdraw(k, sink, rally, planner);
        Fleet(k, sink, knownEnemy);
    }

    // ---- establish --------------------------------------------------------------------------------------

    private void Establish(RecruitKnowledge k, ICommandSink sink)
    {
        ViewActor? ship = null;
        foreach (ViewActor u in k.Utility) if (u.TypeIndex == _commandShip) { ship = u; break; }
        if (ship is not ViewActor s) return;
        PlayerView v = k.View;
        int tick = v.Tick;
        if (_homeRegion == 0) _homeRegion = v.NavalRegionAt(s.Cell);
        if (_siteSet && s.Cell == _siteCell)
        {
            if (tick - _deployTick < DeployWaitTicks) return;
            if (_deployTries >= 3) { _bad.Add(_siteCell); _siteSet = false; _deployTries = 0; }
            else
            {
                Deploys++; _deployTries++; _deployTick = tick;
                sink.Issue(CommandKind.Deploy, s.Id, ActorKind.Vehicle);
                return;
            }
        }
        if (_siteSet && tick - _siteSince > SiteWaitTicks) { _bad.Add(_siteCell); _siteSet = false; }
        if (!_siteSet && !ChooseSite(k, s)) { Debug = "no landing site"; return; }
        if (s.Idle && tick - _moveTick >= MoveGapTicks && s.Cell != _siteCell)
        {
            _moveTick = tick; SailOrders++;
            sink.Issue(CommandKind.Move, s.Id, ActorKind.Vehicle, _siteCell);
        }
    }

    /// <summary>Picks the sea cell the Command Ship deploys from: nearest to the ship that has a buildable 3x3 footprint touching it, preferring
    /// (among the <see cref="SiteFitScan"/> nearest) one with room for the Anchorage's mask near the depot.</summary>
    private bool ChooseSite(RecruitKnowledge k, ViewActor ship)
    {
        PlayerView v = k.View;
        SimRules rules = k.Rules;
        int region = v.NavalRegionAt(ship.Cell);
        if (region <= 0 || _depot < 0) return false;
        BuildingType depot = rules.Buildings[_depot];
        _cands.Clear();
        for (int y = ship.Cell.Y - SiteRadius; y <= ship.Cell.Y + SiteRadius; y++)
            for (int x = ship.Cell.X - SiteRadius; x <= ship.Cell.X + SiteRadius; x++)
            {
                var c = new CellCoord(x, y);
                if (!v.InMap(c) || v.NavalRegionAt(c) != region || IsBad(c)) continue;
                if (!DepotTop(v, rules, depot, c, out CellCoord tl)) continue;
                _cands.Add((Dist2(c, ship.Cell), x, y, tl.X, tl.Y));
            }
        if (_cands.Count == 0) return false;
        _cands.Sort(static (a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        // tier 0: the Anchorage's water mask fits inside the future beachhead and a landing marker lies near the depot; tier 1: it fits; tier 2: neither
        // (the nearest sea cell with a depot spot; the Anchorage can then never be placed). Ties by distance to the ship.
        int scan = Math.Min(_cands.Count, SiteFitScan);
        int bestTier = 3, pick = -1;
        for (int i = 0; i < scan && bestTier > 0; i++)
        {
            var tl = new CellCoord(_cands[i].Tx, _cands[i].Ty);
            int tier = _anchorage >= 0 && DockFits(v, rules, rules.Buildings[_anchorage], depot, tl) ? (MarkerNear(v, tl, depot) ? 0 : 1) : 2;
            if (tier >= bestTier) continue;
            var c = new CellCoord(_cands[i].X, _cands[i].Y);
            if (c != ship.Cell && !v.CanOwnShipReach(ship.Id, c)) continue;
            bestTier = tier; pick = i;
        }
        if (pick < 0)
            for (int i = 0; i < _cands.Count && pick < 0; i++)
                if (new CellCoord(_cands[i].X, _cands[i].Y) == ship.Cell || v.CanOwnShipReach(ship.Id, new CellCoord(_cands[i].X, _cands[i].Y))) { pick = i; bestTier = 2; }
        if (pick < 0) return false;
        _siteCell = new CellCoord(_cands[pick].X, _cands[pick].Y); _siteSet = true; _siteSince = v.Tick; _deployTries = 0; _deployTick = -100000; _moveTick = -100000;
        Debug = $"site {_siteCell} depot {_cands[pick].Tx},{_cands[pick].Ty} tier {bestTier} markers {v.LandingMarkers.Count}";
        return true;
    }

    /// <summary>True when a map landing marker lies within <see cref="MarkerCells"/> cells of the depot footprint.</summary>
    private static bool MarkerNear(PlayerView v, CellCoord depotTl, BuildingType depot)
    {
        foreach (CellCoord m in v.LandingMarkers)
        {
            int dx = Math.Max(0, Math.Max(depotTl.X - m.X, m.X - (depotTl.X + depot.FootprintW - 1)));
            int dy = Math.Max(0, Math.Max(depotTl.Y - m.Y, m.Y - (depotTl.Y + depot.FootprintH - 1)));
            if (Math.Max(dx, dy) <= MarkerCells) return true;
        }
        return false;
    }

    private bool IsBad(CellCoord c)
    {
        foreach (CellCoord b in _bad) if (b == c) return true;
        return false;
    }

    /// <summary>The first top-left (rows, then columns; the sim's own scan) whose footprint is buildable land and touches sea cell <paramref name="sea"/>.</summary>
    private static bool DepotTop(PlayerView v, SimRules rules, BuildingType depot, CellCoord sea, out CellCoord topLeft)
    {
        for (int ty = sea.Y - depot.FootprintH; ty <= sea.Y + 1; ty++)
            for (int tx = sea.X - depot.FootprintW; tx <= sea.X + 1; tx++)
                if (LandFree(v, rules, tx, ty, depot.FootprintW, depot.FootprintH)) { topLeft = new CellCoord(tx, ty); return true; }
        topLeft = default;
        return false;
    }

    private static bool LandFree(PlayerView v, SimRules rules, int x0, int y0, int w, int h)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
            {
                var c = new CellCoord(x, y);
                if (!v.InMap(c) || !rules.Terrain.Buildable(v.LandAt(c)) || v.OverlayAt(c) != OverlayKind.None) return false;
            }
        return true;
    }

    /// <summary>True when a water structure of this type (its mask: water where 1, buildable land where 0) fits within
    /// <see cref="AnchorageReach"/> cells of a depot whose top-left is <paramref name="depotTl"/>.</summary>
    private static bool DockFits(PlayerView v, SimRules rules, BuildingType dock, BuildingType depot, CellCoord depotTl)
    {
        int w = dock.FootprintW, h = dock.FootprintH;
        bool mask = dock.WaterMask.Count == w * h;
        for (int ty = depotTl.Y - AnchorageReach - h; ty <= depotTl.Y + depot.FootprintH + AnchorageReach; ty++)
            for (int tx = depotTl.X - AnchorageReach - w; tx <= depotTl.X + depot.FootprintW + AnchorageReach; tx++)
            {
                int gx = Math.Max(0, Math.Max(depotTl.X - (tx + w - 1), tx - (depotTl.X + depot.FootprintW - 1)));
                int gy = Math.Max(0, Math.Max(depotTl.Y - (ty + h - 1), ty - (depotTl.Y + depot.FootprintH - 1)));
                if (Math.Max(gx, gy) > AnchorageReach || (gx == 0 && gy == 0)) continue;
                bool ok = true;
                for (int dy = 0; dy < h && ok; dy++)
                    for (int dx = 0; dx < w && ok; dx++)
                    {
                        var c = new CellCoord(tx + dx, ty + dy);
                        if (!v.InMap(c)) { ok = false; break; }
                        LandType land = v.LandAt(c);
                        bool water = land is LandType.Water or LandType.Shallow;
                        bool want = !mask || dock.WaterMask[dy * w + dx];
                        ok = want ? water : !water && rules.Terrain.Buildable(land) && v.OverlayAt(c) == OverlayKind.None;
                    }
                if (ok) return true;
            }
        return false;
    }

    // ---- withdraw ---------------------------------------------------------------------------------------

    private void EvaluateWithdraw(RecruitKnowledge k, ICommandSink sink, CellCoord rally, TierPlanner planner)
    {
        int tick = k.View.Tick;
        long own = planner.OwnArmyValue, enemy = planner.EnemyArmyLong;
        if (!_withdraw)
        {
            if (enemy >= WithdrawMinEnemy && own * 100 < enemy * WithdrawPct && tick - _withdrawEnd >= WithdrawCooldownTicks)
            {
                _withdraw = true; _withdrawSince = tick; Withdrawals++;
                _military?.AbortWave(k, sink, rally);
            }
        }
        else if (own * 100 >= enemy * WithdrawResumePct || tick - _withdrawSince >= WithdrawMaxTicks)
        {
            _withdraw = false; _withdrawEnd = tick;
        }
        if (_military is not null) _military.Hold = _withdraw;
    }

    // ---- fleet: escorts, retreat, reserve, strikes ------------------------------------------------------

    private void Fleet(RecruitKnowledge k, ICommandSink sink, List<ViewActor> knownEnemy)
    {
        PlayerView v = k.View;
        SimRules rules = k.Rules;
        int tick = v.Tick;
        Prune(_escort, k.Fleet); Prune(_hurt, k.Fleet);
        CellCoord? homeSea = NearestSea(v, k.Yard, _homeRegion, 30, false);
        if (homeSea is not CellCoord hw) return;

        // retreat and repair
        foreach (ViewActor s in k.Fleet)
        {
            ulong key = RecruitKnowledge.KeyOf(s);
            bool weak = s.MaxHealth > 0 && s.Health * 100 < s.MaxHealth * RetreatPct;
            if (_hurt.Contains(key))
            {
                if (s.Health * 100 >= s.MaxHealth * RepairDonePct) _hurt.Remove(key);
            }
            else if (weak && !s.Submerged)
            {
                _hurt.Add(key); ShipRetreats++;
                MoveShip(k, sink, s, RepairSea(k, s, hw));
            }
        }
        foreach (ViewActor s in k.Fleet)
            if (_hurt.Contains(RecruitKnowledge.KeyOf(s)) && s.Idle && Cheb(s.Cell, RepairSea(k, s, hw)) > 2)
                MoveShip(k, sink, s, RepairSea(k, s, hw));

        // convoy escorts
        bool dock = _anchorage >= 0 && k.BuildingCount[_anchorage] > 0;
        Convoy(k, sink, dock, hw);

        // the rest: reserve at home, strike group out
        _pool.Clear();
        foreach (ViewActor s in k.Fleet)
        {
            ulong key = RecruitKnowledge.KeyOf(s);
            if (_hurt.Contains(key) || _escort.Contains(key) || IsCarrier(rules, s)) continue;
            _pool.Add(s);
        }
        int reserve = _pool.Count >= 5 ? ReserveFrom5 : _pool.Count >= ReserveMin ? ReserveFrom3 : 0;
        _group.Clear(); _free.Clear();
        for (int i = 0; i < _pool.Count; i++) (i < reserve ? _free : _group).Add(_pool[i]);
        foreach (ViewActor s in k.Fleet) if (IsCarrier(rules, s) && !_hurt.Contains(RecruitKnowledge.KeyOf(s))) _free.Add(s);   // carriers stay behind their sorties

        foreach (ViewActor s in _free)
            if (s.Idle && Cheb(s.Cell, hw) > 6) MoveShip(k, sink, s, hw);

        SubHunt(k, sink);
        Debug = $"fleet {k.Fleet.Count} pool {_pool.Count} group {_group.Count} escort {_escort.Count} hurt {_hurt.Count} withdraw {_withdraw} strike {_strikeSet}/{_probe} strikes {Strikes} withdrawals {Withdrawals}";
        if (_withdraw || _group.Count < StrikeMin)
        {
            _strikeSet = false;
            foreach (ViewActor s in _group) if (s.Idle && Cheb(s.Cell, hw) > 6) MoveShip(k, sink, s, hw);
            return;
        }
        Strike(k, sink, knownEnemy, hw);
    }

    private static bool IsCarrier(SimRules rules, ViewActor s) => rules.Vehicles[s.TypeIndex].AircraftSlots > 0;

    /// <summary>The sea cell a hurt ship goes to: next to a Mender when one is on the water's edge, else home.</summary>
    private CellCoord RepairSea(RecruitKnowledge k, ViewActor ship, CellCoord homeSea)
    {
        long best = long.MaxValue;
        CellCoord spot = homeSea;
        foreach (ViewActor m in k.Support)
        {
            if (m.Kind != ActorKind.Vehicle || k.Rules.Vehicles[m.TypeIndex].AuraRepairPermille <= 0) continue;
            CellCoord? w = NearestSea(k.View, m.Cell, _homeRegion, 4, false);
            if (w is not CellCoord c) continue;
            long d = Dist2(c, ship.Cell);
            if (d < best) { best = d; spot = c; }
        }
        return spot;
    }

    private void Prune(HashSet<ulong> set, List<ViewActor> alive)
    {
        if (set.Count == 0) return;
        _dead.Clear();
        foreach (ulong key in set)
        {
            bool found = false;
            foreach (ViewActor a in alive) if (RecruitKnowledge.KeyOf(a) == key) { found = true; break; }
            if (!found) _dead.Add(key);
        }
        foreach (ulong key in _dead) set.Remove(key);
    }

    // ---- convoy -----------------------------------------------------------------------------------------

    private void Convoy(RecruitKnowledge k, ICommandSink sink, bool dock, CellCoord homeSea)
    {
        PlayerView v = k.View;
        int tick = v.Tick;
        if (!dock) { _escort.Clear(); return; }
        int eta = v.ShipmentEtaTicks;
        bool active = _tenders.Count > 0 || (eta >= 0 && eta <= EscortEtaTicks);
        if (active) _lastConvoyTick = tick;
        if (!active && tick - _lastConvoyTick > EscortLingerTicks) { _escort.Clear(); return; }

        ViewActor anchor = default;
        foreach (ViewActor b in k.Buildings) if (b.TypeIndex == _anchorage) { anchor = b; break; }
        CellCoord station = NearestSea(v, anchor.Cell, _homeRegion, 8, false) ?? homeSea;

        // a hurt tender, or an enemy near one: the escorts go to it
        CellCoord? alert = null;
        foreach (TenderView t in _tenders)
        {
            bool hurt = false;
            foreach (ViewActor a in k.Own) if (a.Kind == ActorKind.Vehicle && a.Id == t.Vehicle && a.Health < a.MaxHealth) { hurt = true; break; }
            bool threat = false;
            foreach (ViewActor e in k.Visible)
                if (e.Relation == Relation.Enemy && e.IsMobile && Cheb(e.Cell, t.Cell) <= TenderDangerCells) { threat = true; break; }
            if (hurt || threat) { alert = t.Cell; ConvoyAlerts++; break; }
        }

        int wanted = Tier >= 3 ? 2 : 1;
        // keep the escorts that are alive, add the nearest free ships until the count is met
        while (_escort.Count < wanted)
        {
            ViewActor? best = null;
            long bd = long.MaxValue;
            foreach (ViewActor s in k.Fleet)
            {
                ulong key = RecruitKnowledge.KeyOf(s);
                if (_escort.Contains(key) || _hurt.Contains(key) || IsCarrier(k.Rules, s)) continue;
                long d = Dist2(s.Cell, station);
                if (d < bd) { bd = d; best = s; }
            }
            if (best is not ViewActor pick) break;
            _escort.Add(RecruitKnowledge.KeyOf(pick));
        }
        foreach (ViewActor s in k.Fleet)
        {
            if (!_escort.Contains(RecruitKnowledge.KeyOf(s))) continue;
            if (alert is CellCoord ac)
            {
                if (s.Idle || Cheb(s.Cell, ac) > TenderDangerCells) { EscortOrders++; AttackMoveShip(k, sink, s, ac); }
            }
            else if (s.Idle && Cheb(s.Cell, station) > StationCells)
            {
                EscortOrders++; MoveShip(k, sink, s, station);
            }
        }
    }

    // ---- strike -----------------------------------------------------------------------------------------

    private void Strike(RecruitKnowledge k, ICommandSink sink, List<ViewActor> knownEnemy, CellCoord homeSea)
    {
        PlayerView v = k.View;
        SimRules rules = k.Rules;
        int tick = v.Tick;
        long cx = 0, cy = 0;
        int range = int.MaxValue;
        bool deep = false;
        foreach (ViewActor s in _group)
        {
            cx += s.Cell.X; cy += s.Cell.Y;
            VehicleType vt = rules.Vehicles[s.TypeIndex];
            deep |= vt.DeepOnly;
            if (vt.PrimaryWeapon >= 0) range = Math.Min(range, rules.Weapons[vt.PrimaryWeapon].Range);
        }
        if (range == int.MaxValue) return;
        var centre = new CellCoord((int)(cx / _group.Count), (int)(cy / _group.Count));

        // keep the target while it is known and the review time has not passed
        bool keep = false;
        if (_strikeSet && tick - _strikeSince < StrikeReviewTicks)
        {
            if (_probe) keep = true;
            else foreach (ViewActor b in knownEnemy) if (b.Cell == _strikeBuilding) { keep = true; break; }
        }
        if (!keep && !ChooseStrike(k, knownEnemy, centre, range, deep) && !ChooseProbe(k, centre, deep))
        {
            _strikeSet = false;
            foreach (ViewActor s in _group) if (s.Idle && Cheb(s.Cell, homeSea) > 6) MoveShip(k, sink, s, homeSea);
            return;
        }

        // the live building (for an explicit Attack) when it is in view
        ViewActor? target = null;
        if (!_probe)
            foreach (ViewActor b in knownEnemy) if (b.Cell == _strikeBuilding && !b.Ghost) { target = b; break; }
        bool focus = _profile is { FocusFire: true };
        foreach (ViewActor s in _group)
        {
            VehicleType vt = rules.Vehicles[s.TypeIndex];
            int reach = vt.PrimaryWeapon >= 0 ? rules.Weapons[vt.PrimaryWeapon].Range : 0;
            // focus fire: the weakest visible enemy inside the group's gun range
            if (focus && MilitaryManager.PickFocus(k.Visible, s.Cell, rules) is ViewActor f && f.Kind != ActorKind.Building
                && Dist2L(s.Position, f.Position) <= (long)reach * reach)
            {
                if (s.Target != f.Id) sink.Issue(CommandKind.Attack, s.Id, ActorKind.Vehicle, default, f.Ref);
                continue;
            }
            bool there = Cheb(s.Cell, _strikeCell) <= 2;
            if (there && target is ViewActor tb && Dist2L(s.Position, tb.Position) <= (long)reach * reach)
            {
                if (s.Target != tb.Id) sink.Issue(CommandKind.Attack, s.Id, ActorKind.Vehicle, default, tb.Ref);
            }
            else if (s.Idle && !there) AttackMoveShip(k, sink, s, _strikeCell);
        }
    }

    /// <summary>Chooses the known enemy building whose firing position (a sea cell in the group's sea, within the stand-off range of the building)
    /// is nearest to the group. Looks at the <see cref="TargetsLooked"/> buildings nearest to the base.</summary>
    private bool ChooseStrike(RecruitKnowledge k, List<ViewActor> knownEnemy, CellCoord centre, int range, bool deep)
    {
        PlayerView v = k.View;
        int region = v.NavalRegionAt(centre, deep);
        if (region <= 0) return false;
        _cands.Clear();
        for (int i = 0; i < knownEnemy.Count; i++)
        {
            BuildingType bt = k.Rules.Buildings[knownEnemy[i].TypeIndex];
            if (bt.Wall || bt.Gate) continue;
            _cands.Add((Dist2(knownEnemy[i].Cell, k.Yard), i, 0, 0, 0));
        }
        _cands.Sort(static (a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.X.CompareTo(b.X));
        long bestScore = long.MaxValue;
        CellCoord bestCell = default, bestBuilding = default;
        int standOff = range * StandOffPct / 100;
        long standSq = (long)standOff * standOff;
        for (int n = 0; n < _cands.Count && n < TargetsLooked; n++)
        {
            ViewActor b = knownEnemy[_cands[n].X];
            BuildingType bt = k.Rules.Buildings[b.TypeIndex];
            int reachCells = Math.Min(ScanCells, standOff / 256 + 1);
            int bx = b.Cell.X * 256 + bt.FootprintW * 128, by = b.Cell.Y * 256 + bt.FootprintH * 128;
            for (int y = b.Cell.Y - reachCells; y <= b.Cell.Y + bt.FootprintH + reachCells; y++)
                for (int x = b.Cell.X - reachCells; x <= b.Cell.X + bt.FootprintW + reachCells; x++)
                {
                    var c = new CellCoord(x, y);
                    if (!v.InMap(c) || v.NavalRegionAt(c, deep) != region) continue;
                    long dx = c.X * 256L + 128 - bx, dy = c.Y * 256L + 128 - by;
                    if (dx * dx + dy * dy > standSq) continue;
                    long score = Dist2(c, centre);
                    if (score < bestScore || (score == bestScore && (c.Y < bestCell.Y || (c.Y == bestCell.Y && c.X < bestCell.X))))
                    { bestScore = score; bestCell = c; bestBuilding = b.Cell; }
                }
        }
        if (bestScore == long.MaxValue) return false;
        if (!_strikeSet || _probe || _strikeBuilding != bestBuilding) Strikes++;
        _probe = false;
        _strikeSet = true; _strikeCell = bestCell; _strikeBuilding = bestBuilding; _strikeSince = v.Tick;
        return true;
    }

    /// <summary>S11: no enemy building is known (or none lies within gun range of the group's sea): the group sails to the sea cell nearest to the enemy
    /// start (<see cref="_front"/>) in its own sea and attack-moves there. It sees what lies on the coast on the way, fires at it, and the next review
    /// picks a real building target. Only the terrain (public) is read.</summary>
    private bool ChooseProbe(RecruitKnowledge k, CellCoord centre, bool deep)
    {
        PlayerView v = k.View;
        int region = v.NavalRegionAt(centre, deep);
        if (region <= 0) return false;
        long best = long.MaxValue;
        CellCoord pick = default;
        for (int y = 0; y < _mapH; y++)
            for (int x = 0; x < _mapW; x++)
            {
                var c = new CellCoord(x, y);
                if (v.NavalRegionAt(c, deep) != region) continue;
                long d = Dist2(c, _front);
                if (d < best) { best = d; pick = c; }
            }
        if (best == long.MaxValue) return false;
        if (!_strikeSet || !_probe || _strikeCell != pick) Strikes++;
        _probe = true; _strikeSet = true; _strikeCell = pick; _strikeBuilding = default; _strikeSince = v.Tick;
        return true;
    }

    // ---- sub hunting and carriers -----------------------------------------------------------------------

    private void SubHunt(RecruitKnowledge k, ICommandSink sink)
    {
        if (Tier < 2) return;
        ViewActor? sub = null;
        long best = long.MaxValue;
        foreach (ViewActor e in k.Visible)
        {
            if (e.Relation != Relation.Enemy || !e.Submerged) continue;
            long d = Dist2(e.Cell, k.Yard);
            if (d < best) { best = d; sub = e; }
        }
        if (sub is not ViewActor target) return;
        foreach (ViewActor s in k.Fleet)
        {
            VehicleType vt = k.Rules.Vehicles[s.TypeIndex];
            if (vt.PrimaryWeapon < 0) continue;
            WeaponTargets t = k.Rules.Weapons[vt.PrimaryWeapon].Targets;
            if (t is not (WeaponTargets.Submerged or WeaponTargets.NavalAndSubmerged) || _hurt.Contains(RecruitKnowledge.KeyOf(s))) continue;
            if (s.Target == target.Id) continue;
            SubHunts++;
            sink.Issue(CommandKind.Attack, s.Id, ActorKind.Vehicle, default, target.Ref);
        }
    }

    private void Carriers(RecruitKnowledge k, ICommandSink sink)
    {
        PlayerView v = k.View;
        SimRules rules = k.Rules;
        int tick = v.Tick;
        ViewActor? carrierFree = null;
        foreach (ViewActor c in k.Fleet)
        {
            if (!IsCarrier(rules, c) || !v.OwnCarrierHangar(c.Id, out int docked, out int slots)) continue;
            if (docked < slots && carrierFree is null) carrierFree = c;
            ulong key = RecruitKnowledge.KeyOf(c);
            if (docked <= 0 || _hurt.Contains(key) || _withdraw) continue;
            if (_lastSortie.TryGetValue(key, out int last) && tick - last < SortieGapTicks) continue;
            // nearest visible enemy (not air) near the carrier, else the strike building when it is in reach
            ViewActor? foe = null;
            long best = long.MaxValue;
            foreach (ViewActor e in k.Visible)
            {
                if (e.Relation != Relation.Enemy || e.Underground) continue;
                if (e.Kind == ActorKind.Vehicle && rules.Vehicles[e.TypeIndex].Locomotion == Locomotion.Air) continue;
                long d = Dist2(e.Cell, c.Cell);
                if (d < best && Cheb(e.Cell, c.Cell) <= SortieCells) { best = d; foe = e; }
            }
            if (foe is not ViewActor target) continue;
            _lastSortie[key] = tick; Sorties++;
            sink.Issue(CommandKind.Attack, c.Id, ActorKind.Vehicle, default, target.Ref);
        }
        // new aircraft go straight into a carrier with room: the flight decks rally on it
        foreach (ViewActor b in k.Buildings)
        {
            BuildingType bt = rules.Buildings[b.TypeIndex];
            if (bt.Produces is not ProductionCategory.Aircraft) continue;
            ulong key = RecruitKnowledge.KeyOf(b);
            CellCoord want = carrierFree is ViewActor cf ? cf.Cell : k.Yard;
            int tag = carrierFree is ViewActor ? 1 : 0;
            if (_deckRally.TryGetValue(key, out int t0) && t0 == tag && (tag == 0 || _deckCell.TryGetValue(key, out CellCoord cc) && Cheb(cc, want) <= 2)) continue;
            _deckRally[key] = tag; _deckCell[key] = want;
            if (tag == 1) sink.Rally(b.Id, want);
        }
    }

    // ---- ship orders ------------------------------------------------------------------------------------

    private bool Reachable(PlayerView v, ViewActor ship, CellCoord c)
    {
        bool deep = v.Rules.Vehicles[ship.TypeIndex].DeepOnly;
        int r = v.NavalRegionAt(c, deep);
        return r > 0 && r == v.NavalRegionAt(ship.Cell, deep);
    }

    private void MoveShip(RecruitKnowledge k, ICommandSink sink, ViewActor ship, CellCoord c)
    {
        if (!Reachable(k.View, ship, c)) { Dropped++; return; }
        sink.Issue(CommandKind.Move, ship.Id, ActorKind.Vehicle, c);
    }

    private void AttackMoveShip(RecruitKnowledge k, ICommandSink sink, ViewActor ship, CellCoord c)
    {
        if (!Reachable(k.View, ship, c)) { Dropped++; return; }
        sink.Issue(CommandKind.AttackMove, ship.Id, ActorKind.Vehicle, c);
    }

    /// <summary>The sea cell nearest to <paramref name="from"/> (rings outward, rows then columns) in sea <paramref name="region"/> (0: any sea).</summary>
    private static CellCoord? NearestSea(PlayerView v, CellCoord from, int region, int maxRadius, bool deep)
    {
        for (int r = 0; r <= maxRadius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(from.X + dx, from.Y + dy);
                    if (!v.InMap(c)) continue;
                    int cr = v.NavalRegionAt(c, deep);
                    if (cr > 0 && (region == 0 || cr == region)) return c;
                }
        return null;
    }

    private static int Cheb(CellCoord a, CellCoord b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
    private static long Dist2(CellCoord a, CellCoord b) { long dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }
    private static long Dist2L(LeptonCoord a, LeptonCoord b) { long dx = a.X - b.X, dy = a.Y - b.Y; return dx * dx + dy * dy; }

    // ---- save / restore (written only for an Expedition side, so older AI blobs are unchanged) ----

    /// <summary>Writes everything the tactics remembers between decisions.</summary>
    internal void Snapshot(BinaryWriter w)
    {
        w.Write(_homeRegion); w.Write(_siteSet); AiSnapshot.WriteCell(w, _siteCell);
        w.Write(_siteSince); w.Write(_deployTick); w.Write(_moveTick); w.Write(_deployTries);
        w.Write(_bad.Count); foreach (CellCoord c in _bad) AiSnapshot.WriteCell(w, c);
        AiSnapshot.WriteKeys(w, _escort); AiSnapshot.WriteKeys(w, _hurt);
        w.Write(_lastConvoyTick); w.Write(_strikeSet); w.Write(_probe); AiSnapshot.WriteCell(w, _strikeCell); AiSnapshot.WriteCell(w, _strikeBuilding); w.Write(_strikeSince);
        AiSnapshot.WriteMap(w, _lastSortie, (bw, x) => bw.Write(x));
        AiSnapshot.WriteMap(w, _deckRally, (bw, x) => bw.Write(x));
        AiSnapshot.WriteMap(w, _deckCell, (bw, x) => AiSnapshot.WriteCell(bw, x));
        w.Write(_withdraw); w.Write(_withdrawSince); w.Write(_withdrawEnd); w.Write(_enemySubSeen);
        w.Write(Deploys); w.Write(SailOrders); w.Write(Dropped); w.Write(Strikes); w.Write(EscortOrders); w.Write(ConvoyAlerts);
        w.Write(ShipRetreats); w.Write(Withdrawals); w.Write(Sorties); w.Write(SubHunts); w.Write(BeaconsPlaced); w.Write(Established);
    }

    /// <summary>Reads what <see cref="Snapshot"/> wrote.</summary>
    internal void Restore(BinaryReader r)
    {
        _homeRegion = r.ReadInt32(); _siteSet = r.ReadBoolean(); _siteCell = AiSnapshot.ReadCell(r);
        _siteSince = r.ReadInt32(); _deployTick = r.ReadInt32(); _moveTick = r.ReadInt32(); _deployTries = r.ReadInt32();
        int n = AiSnapshot.Count(r);
        _bad.Clear(); for (int i = 0; i < n; i++) _bad.Add(AiSnapshot.ReadCell(r));
        AiSnapshot.ReadKeys(r, _escort); AiSnapshot.ReadKeys(r, _hurt);
        _lastConvoyTick = r.ReadInt32(); _strikeSet = r.ReadBoolean(); _probe = r.ReadBoolean(); _strikeCell = AiSnapshot.ReadCell(r); _strikeBuilding = AiSnapshot.ReadCell(r); _strikeSince = r.ReadInt32();
        AiSnapshot.ReadMap(r, _lastSortie, br => br.ReadInt32());
        AiSnapshot.ReadMap(r, _deckRally, br => br.ReadInt32());
        AiSnapshot.ReadMap(r, _deckCell, br => AiSnapshot.ReadCell(br));
        _withdraw = r.ReadBoolean(); _withdrawSince = r.ReadInt32(); _withdrawEnd = r.ReadInt32(); _enemySubSeen = r.ReadBoolean();
        Deploys = r.ReadInt32(); SailOrders = r.ReadInt32(); Dropped = r.ReadInt32(); Strikes = r.ReadInt32(); EscortOrders = r.ReadInt32(); ConvoyAlerts = r.ReadInt32();
        ShipRetreats = r.ReadInt32(); Withdrawals = r.ReadInt32(); Sorties = r.ReadInt32(); SubHunts = r.ReadInt32(); BeaconsPlaced = r.ReadInt32(); Established = r.ReadBoolean();
    }
}
