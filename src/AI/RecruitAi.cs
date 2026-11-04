// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Rules;
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>
/// "recruit": the tier 1 economy skirmish AI. Honest (sees only its <see cref="PlayerView"/>), slow and simple:
/// a fixed structure build order, a seeded unit mix, one harvester that is never replaced, no repair, and slow attack
/// waves (see the manager classes in Rts.AI.Recruit). Deterministic: no state from outside the view, decisions on the
/// profile's interval.
/// </summary>
public sealed class RecruitAi : IAiPlayer, IAiSnapshotable
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
    // M4-12: garrisons and the no-income rescue
    private readonly GarrisonManager _garrison = new();
    private readonly EconomyRescue _rescue = new();
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

    /// <summary>M4-12: Enter orders given to fill garrisons.</summary>
    public int GarrisonOrders => _garrison.Orders;

    /// <summary>M4-12: most own soldiers inside garrisons at once.</summary>
    public int PeakGarrisoned => _garrison.PeakOccupants;

    /// <summary>M4-12: structures sold to pay for a harvester after every harvester was lost.</summary>
    public int StructuresSold => _rescue.Sold;

    /// <summary>M4-16: decisions on which tech spending was held because the known enemy army was bigger.</summary>
    public int TechHolds => _build?.TechSkips ?? 0;

    /// <summary>M4-12: the enemy player waves go for (-1 while none is known).</summary>
    public int TargetPlayer => _military.TargetPlayer;

    /// <summary>M4-12 (diagnostics): the wave manager's state in one line.</summary>
    public string MilitaryDebug => _military.Debug;

    /// <summary>M4-12: units sent after a wave already on the attack.</summary>
    public int Reinforcements => _military.Reinforcements;

    /// <summary>M4-12: waves ended because they destroyed nothing for a long time.</summary>
    public int StaleWaves => _military.StaleWaves;

    /// <summary>M4-12: highest tech tier the player has reached, and the first tick it reached tier 2 / tier 3 (-1 = never).</summary>
    public int MaxTier { get; private set; } = 1;

    /// <summary>M4-12: first tick at tech tier 2 (-1 = never).</summary>
    public int Tier2Tick { get; private set; } = -1;

    /// <summary>M4-12: first tick at tech tier 3 (-1 = never).</summary>
    public int Tier3Tick { get; private set; } = -1;

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
        _build = new BuildOrderManager(_faction.OrderFor(_profile.BuildRepertoire), _faction.PowerId, _faction.YardId)
        { ReserveInProgress = _profile.RespondToAttacks };   // M4-12 (tier 2+)
        _planner = new TierPlanner(_profile, _faction);
        _build.HoldStep = id => _planner.Behind && _planner.IsTech(id);   // M4-16
        _production = new ProductionManager(_faction, _tactics, _planner);
        _production.Start(_ctx!.Rng);
        _military.Tactics = _tactics;
        _tactics?.Start(_ctx);
    }

    /// <summary>M4-12: no harvester alive and the funds do not cover a new one: only an attack can still win the game.</summary>
    private bool AllIn()
    {
        if (_rescue.Broke) return true;   // income stalled (map mined out): the garrison joins the last army
        if (_k.Harvesters.Count > 0) return false;
        int harv = SimRules.IndexOf(_k.Rules.Vehicles, _faction!.HarvesterId, v => v.Id);
        return harv < 0 || _k.Hud.Cash + _k.Hud.Stored < _k.Rules.Vehicles[harv].Cost
            || !_k.Buildable(ProductionCategory.Vehicle, harv);
    }

    /// <inheritdoc />
    public void Think(PlayerView view, ICommandSink sink)
    {
        if (_faction is null) Init(view.Faction);
        _k.Refresh(view, _faction!.YardId);
        if (_k.Hud.Defeated) return;
        int tier = view.Tier;
        if (tier >= 2 && Tier2Tick < 0) Tier2Tick = view.Tick;
        if (tier >= 3 && Tier3Tick < 0) Tier3Tick = view.Tick;
        if (tier > MaxTier) MaxTier = tier;
        view.CopyKnownEnemyBuildings(_knownBuildings);
        _squads.Apply(_k);
        // M5-02: the commander's directive, as knob values for this decision (Directive.None gives the values the managers always used)
        DirectiveBias bias = DirectiveBias.For(view.Directive);
        _military.Bias = bias; _production!.Bias = bias; _planner!.Bias = bias; _upgrades.Bias = bias; _garrison.Bias = bias;
        _planner.Observe(_k, _knownBuildings);
        _planner.ExtendBuild(_k, _build!);
        if (_profile.ReplaceHarvesters) _rescue.Think(_k, sink, _faction);   // M4-12: no harvester and no money: sell for one
        _build!.HoldExcept = _rescue.Saving ? _rescue.RefineryType : _rescue.Broke ? -1 : int.MinValue;
        _production!.Hold = _rescue.Saving;
        _build.Think(_k, sink);
        _economy.Think(_k, sink);
        if (_profile.Repair) _repair.Think(_k, sink);
        _production!.Think(_k, sink, _build.PendingCost);
        if (_profile.UseUpgrades) _upgrades.Think(_k, sink, _build.PendingCost, _planner.Behind);   // M4-16: no research while behind
        // squads: pull units out of the wave pool before the military manager counts and orders it
        _retreat.Think(_k, sink, _squads, _production.Rally, bias.RetreatPct(_profile.RetreatHealthPct));
        if (_profile.RespondToAttacks) _garrison.Think(_k, sink, AllIn());   // M4-12: before the waves count the army
        if (_profile.ActiveScouting) _scout.Think(_k, sink, _squads);
        if (_profile.Harass) _harass.Think(_k, sink, _squads, _planner, _knownBuildings, _production.Rally, _profile.ScoutInfoAgeTicks);
        _military.Broke = _rescue.Broke;
        _military.Ahead = _planner.Ahead;   // M4-16
        _military.Think(_k, sink, _production.Rally, _knownBuildings);
        if (_profile.Scatter) _scatter.Think(_k, sink);
        if (_profile.Superweapons) _superweapons.Think(_k, sink, _knownBuildings);
        _tactics?.Think(_k, sink, _military.Goal(_k, _knownBuildings), _production.Rally, _military.WaveCentre(_k));
    }

    // ---- M3-21: save / restore (appended; the managers hold their own Snapshot / Restore at the end of their classes) ----

    /// <summary>Layout version of <see cref="SaveState"/>; a blob of another version is not read.</summary>
    private const int StateVersion = 1;

    /// <inheritdoc />
    public void SaveState(BinaryWriter w)
    {
        w.Write(StateVersion);
        w.Write(_id);
        bool thinking = _faction is not null && _build is not null && _production is not null && _planner is not null && _ctx is not null;
        w.Write(thinking);
        if (!thinking) return;   // never decided anything: a fresh AI is the same AI
        w.Write((byte)_faction!.Faction);
        w.Write(_ctx!.Rng.State);
        w.Write(MaxTier); w.Write(Tier2Tick); w.Write(Tier3Tick);
        _build!.Snapshot(w);
        _planner!.Snapshot(w);
        _production!.Snapshot(w);
        _military.Snapshot(w);
        _squads.Snapshot(w);
        _scout.Snapshot(w);
        _harass.Snapshot(w);
        _upgrades.Snapshot(w);
        _rescue.Snapshot(w);
        _garrison.Snapshot(w);
        _repair.Snapshot(w);
        _scatter.Snapshot(w);
        _retreat.Snapshot(w);
        _superweapons.Snapshot(w);
        w.Write(_tactics is DirectorateTactics);
        if (_tactics is DirectorateTactics d) d.Snapshot(w);
    }

    /// <inheritdoc />
    public void LoadState(BinaryReader r)
    {
        if (_ctx is null) throw new InvalidOperationException("LoadState needs Start first.");
        if (r.ReadInt32() != StateVersion) throw new InvalidDataException("AI state of another layout");
        if (r.ReadString() != _id) throw new InvalidDataException("AI state of another AI");
        if (!r.ReadBoolean()) return;
        Init((Faction)r.ReadByte());   // builds the faction-dependent managers exactly as the first decision does
        _ctx.Rng.State = r.ReadUInt64();
        MaxTier = r.ReadInt32(); Tier2Tick = r.ReadInt32(); Tier3Tick = r.ReadInt32();
        _build!.Restore(r);
        _planner!.Restore(r);
        _production!.Restore(r);
        _military.Restore(r);
        _squads.Restore(r);
        _scout.Restore(r);
        _harass.Restore(r);
        _upgrades.Restore(r);
        _rescue.Restore(r);
        _garrison.Restore(r);
        _repair.Restore(r);
        _scatter.Restore(r);
        _retreat.Restore(r);
        _superweapons.Restore(r);
        if (r.ReadBoolean() && _tactics is DirectorateTactics d) d.Restore(r);
    }
}
