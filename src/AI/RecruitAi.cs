// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.AI.Recruit;
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
    private const string YardId = "auth_conyard", PowerId = "auth_power";

    private readonly RecruitKnowledge _k = new();
    private readonly BuildOrderManager _build;
    private readonly ProductionManager _production = new();
    private readonly EconomyManager _economy = new();
    private readonly MilitaryManager _military;
    private readonly List<ViewActor> _knownBuildings = new();

    /// <summary>Creates the AI for a tier's profile.</summary>
    public RecruitAi(DifficultyProfile profile)
    {
        _build = new BuildOrderManager(profile.BuildRepertoire, PowerId, YardId);
        _military = new MilitaryManager(profile.AttackThreshold);
    }

    /// <inheritdoc />
    public string Id => AiRegistry.RecruitId;

    /// <summary>Structures placed.</summary>
    public int StructuresPlaced => _build.Placed;

    /// <summary>Units queued.</summary>
    public int UnitsQueued => _production.Queued;

    /// <summary>Place commands the AI withheld because the view said the cell was illegal (stays 0 when search and rules agree).</summary>
    public int Unplaceable => _build.Unplaceable;

    /// <summary>Attack waves launched.</summary>
    public int Waves => _military.Waves;

    /// <inheritdoc />
    public void Start(AiContext ctx)
    {
        _military.Start(ctx);
        _production.Start(ctx.Rng);
    }

    /// <inheritdoc />
    public void Think(PlayerView view, ICommandSink sink)
    {
        _k.Refresh(view, YardId);
        if (_k.Hud.Defeated) return;
        view.CopyKnownEnemyBuildings(_knownBuildings);
        _build.Think(_k, sink);
        _economy.Think(_k, sink);
        _production.Think(_k, sink, _build.PendingCost);
        _military.Think(_k, sink, _production.Rally, _knownBuildings);
    }
}
