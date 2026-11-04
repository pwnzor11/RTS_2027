// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.AI.Recruit;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>
/// "recruit": the tier 1 economy skirmish AI. Honest (sees only its <see cref="PlayerView"/>), slow and simple:
/// a fixed structure build order, a seeded unit mix, one harvester that is never replaced, no repair, and slow attack
/// waves (see the manager classes in Rts.AI.Recruit). Deterministic: no state from outside the view, decisions on the
/// profile's interval.
/// </summary>
public sealed class RecruitAi : IAiPlayer
{
    private readonly DifficultyProfile _profile;
    private readonly RecruitKnowledge _k = new();
    private readonly EconomyManager _economy = new();
    private readonly MilitaryManager _military;
    private readonly List<ViewActor> _knownBuildings = new();
    private AiContext? _ctx;
    // built on the first Think, when the view says which faction this slot plays
    private RecruitFaction? _faction;
    private BuildOrderManager? _build;
    private ProductionManager? _production;
    private IFactionTactics? _tactics;
    // M3-11: tier capabilities, each gated on a profile flag (see data/ai/difficulty.toml)
    private TierPlanner? _planner;
    private readonly Squads _squads = new();
    private readonly RepairManager _repair = new();
    private readonly ScatterManager _scatter = new();
    private readonly RetreatManager _retreat = new();
    private readonly HarassManager _harass = new();
    private readonly ScoutManager _scout = new();
    private readonly UpgradeManager _upgrades = new();
    private readonly SuperweaponManager _superweapons = new();
    private readonly string _id;

    /// <summary>Creates the AI for a tier's profile. The capabilities (repair, harvester replacement, scouting, ...) come from the
    /// profile's flags; a profile with none set is the tier 1 Recruit.</summary>
    public RecruitAi(DifficultyProfile profile) : this(profile, AiRegistry.RecruitId) { }

    /// <summary>Creates the AI under a registry id (the same AI is registered as "recruit" and as "tiered").</summary>
    public RecruitAi(DifficultyProfile profile, string id)
    {
        _profile = profile;
        _id = id;
        _military = new MilitaryManager(profile.AttackThreshold) { Options = MilitaryOptions.From(profile) };
    }

    /// <inheritdoc />
    public string Id => _id;

    /// <summary>Harvesters built to replace lost ones.</summary>
    public int HarvestersReplaced => _planner?.HarvestersReplaced ?? 0;

    /// <summary>Repair orders given.</summary>
    public int Repairs => _repair.Orders;

    /// <summary>Scatter orders given.</summary>
    public int Scatters => _scatter.Orders;

    /// <summary>Units sent home by the retreat rule.</summary>
    public int Retreats => _retreat.Count;

    /// <summary>Waves called off for losing too much strength.</summary>
    public int GroupRetreats => _military.GroupRetreats;

    /// <summary>Waves launched as two prongs.</summary>
    public int Prongs => _military.Prongs;

    /// <summary>Raiding parties formed.</summary>
    public int RaidParties => _harass.Parties;

    /// <summary>Attack orders given by raiders.</summary>
    public int RaidStrikes => _harass.Strikes;

    /// <summary>Scouts sent.</summary>
    public int Scouts => _scout.Sent;

    /// <summary>Upgrades ordered.</summary>
    public int UpgradesOrdered => _upgrades.Ordered;

    /// <summary>Special (superweapon) commands issued.</summary>
    public int SuperweaponsFired => _superweapons.Fired;

    /// <summary>Aircraft queued.</summary>
    public int AircraftOrdered => _planner?.AircraftOrdered ?? 0;

    /// <summary>Percent of seen enemy units that are heavily armoured (the mix input).</summary>
    public int EnemyHeavyShare => _planner?.HeavyShare ?? 0;

    /// <summary>Structures placed.</summary>
    public int StructuresPlaced => _build?.Placed ?? 0;

    /// <summary>Units queued.</summary>
    public int UnitsQueued => _production?.Queued ?? 0;

    /// <summary>Place commands the AI withheld because the view said the cell was illegal (stays 0 when search and rules agree).</summary>
    public int Unplaceable => _build?.Unplaceable ?? 0;

    /// <summary>Attack waves launched.</summary>
    public int Waves => _military.Waves;

    /// <summary>Directorate: Bores ordered to deploy into Hatches (0 for Authority).</summary>
    public int HatchesDug => (_tactics as DirectorateTactics)?.Digs ?? 0;

    /// <summary>Directorate: Enter orders given for network transit (0 for Authority).</summary>
    public int TunnelOrders => (_tactics as DirectorateTactics)?.Transits ?? 0;

    /// <summary>Directorate: Move orders that took infantry off irradiated cells (0 for Authority).</summary>
    public int RadiationMoves => (_tactics as DirectorateTactics)?.RadiationMoves ?? 0;

    /// <inheritdoc />
    public void Start(AiContext ctx)
    {
        _ctx = ctx;
        _military.Start(ctx);
        _scout.Start(ctx);
    }

    /// <summary>Creates the faction-dependent managers (once, on the first decision). The seeded generator is consumed in the
    /// same order as before the factions were split, so Authority games are unchanged.</summary>
    private void Init(Faction faction)
    {
        _faction = RecruitFaction.For(faction);
        _tactics = _faction.CreateTactics();
        _build = new BuildOrderManager(_faction.OrderFor(_profile.BuildRepertoire), _faction.PowerId, _faction.YardId);
        _planner = new TierPlanner(_profile, _faction);
        _production = new ProductionManager(_faction, _tactics, _planner);
        _production.Start(_ctx!.Rng);
        _military.Tactics = _tactics;
        _tactics?.Start(_ctx);
    }

    /// <inheritdoc />
    public void Think(PlayerView view, ICommandSink sink)
    {
        if (_faction is null) Init(view.Faction);
        _k.Refresh(view, _faction!.YardId);
        if (_k.Hud.Defeated) return;
        view.CopyKnownEnemyBuildings(_knownBuildings);
        _squads.Apply(_k);
        _planner!.Observe(_k, _knownBuildings);
        _planner.ExtendBuild(_k, _build!);
        _build!.Think(_k, sink);
        _economy.Think(_k, sink);
        if (_profile.Repair) _repair.Think(_k, sink);
        _production!.Think(_k, sink, _build.PendingCost);
        if (_profile.UseUpgrades) _upgrades.Think(_k, sink, _build.PendingCost);
        // squads: pull units out of the wave pool before the military manager counts and orders it
        _retreat.Think(_k, sink, _squads, _production.Rally, _profile.RetreatHealthPct);
        if (_profile.ActiveScouting) _scout.Think(_k, sink, _squads);
        if (_profile.Harass) _harass.Think(_k, sink, _squads, _planner, _knownBuildings, _production.Rally, _profile.ScoutInfoAgeTicks);
        _military.Think(_k, sink, _production.Rally, _knownBuildings);
        if (_profile.Scatter) _scatter.Think(_k, sink);
        if (_profile.Superweapons) _superweapons.Think(_k, sink, _knownBuildings);
        _tactics?.Think(_k, sink, _military.Goal(_k, _knownBuildings), _production.Rally, _military.WaveCentre(_k));
    }
}
