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

    /// <summary>Creates the AI for a tier's profile.</summary>
    public RecruitAi(DifficultyProfile profile)
    {
        _profile = profile;
        _military = new MilitaryManager(profile.AttackThreshold);
    }

    /// <summary>Harvest orders to probe cells (a stalled scraper sent to look for a field).</summary>
    public int Probes => _economy.Probes;

    /// <inheritdoc />
    public string Id => AiRegistry.RecruitId;

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
        _economy.Start(ctx);
    }

    /// <summary>Creates the faction-dependent managers (once, on the first decision). The seeded generator is consumed in the
    /// same order as before the factions were split, so Authority games are unchanged.</summary>
    private void Init(Faction faction)
    {
        _faction = RecruitFaction.For(faction);
        _tactics = _faction.CreateTactics();
        _build = new BuildOrderManager(_faction.OrderFor(_profile.BuildRepertoire), _faction.PowerId, _faction.YardId);
        _production = new ProductionManager(_faction, _tactics);
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
        _build!.Think(_k, sink);
        _economy.Think(_k, sink);
        _production!.Think(_k, sink, _build.PendingCost);
        _military.Think(_k, sink, _production.Rally, _knownBuildings);
        _tactics?.Think(_k, sink, _military.Goal(_k, _knownBuildings), _production.Rally, _military.WaveCentre(_k));
    }
}
