// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.View;

namespace Rts.AI.Recruit;

/// <summary>
/// Faction extras of the Recruit framework: what a faction does beyond the shared build / produce / attack loop. The managers call
/// these hooks; a faction without extras passes null. Everything works from the player's view only (fog-honest).
/// </summary>
internal interface IFactionTactics
{
    /// <summary>Called once from AI start.</summary>
    void Start(AiContext ctx);

    /// <summary>Per decision, after the managers: dig and deploy, move support units, keep infantry off irradiated cells.
    /// <paramref name="waveCentre"/> is the centre of the wave on the march (null while none).</summary>
    void Think(RecruitKnowledge k, ICommandSink sink, CellCoord goal, CellCoord rally, LeptonCoord? waveCentre);

    /// <summary>A vehicle type index the production lane should build now instead of its bag, or -1.</summary>
    int WantedVehicle(RecruitKnowledge k);

    /// <summary>A rally / staging cell adjusted to keep non-resistant infantry off irradiated cells.</summary>
    CellCoord SafeCell(RecruitKnowledge k, CellCoord cell);

    /// <summary>The staging cell for a wave heading to <paramref name="goal"/> when a forward entrance exists, else null.</summary>
    CellCoord? ForwardStage(RecruitKnowledge k, CellCoord goal);

    /// <summary>M7-10: the rally cell of production building <paramref name="factory"/> when it differs from the shared one (a naval yard rallies on water), else null.</summary>
    CellCoord? RallyFor(RecruitKnowledge k, ViewActor factory) => null;

    /// <summary>Sends wave member <paramref name="a"/> through the tunnel network to the forward entrance. True when it was ordered
    /// (the caller then issues no other order this decision).</summary>
    bool TryEnter(RecruitKnowledge k, ICommandSink sink, ViewActor a, CellCoord stage);
}
