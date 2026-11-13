// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI.Recruit;
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.View;
using Xunit;

namespace AI.Tests.S23;

/// <summary>s23 D-235 (M13-23): the AI endgame fixes (A, B1, B2, B3), the pure decisions.</summary>
public sealed class EndgameTests
{
    private static SimRules Rules => TestWorld.Rules.Sim;

    private static ViewActor Bld(int slot, string id, int x, int y, bool ghost = false) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Building, 1, Relation.Enemy, SimRules.IndexOf(Rules.Buildings, id, b => b.Id), new CellCoord(x, y),
            new LeptonCoord(x * 256, y * 256), 100, 100, MissionKind.Guard, false, EntityId.None, ghost, 1, 1);

    private static ViewActor Unit(int slot, MissionKind m) =>
        new(new EntityId((ushort)slot, 1), ActorKind.Infantry, 0, Relation.Self, 0, new CellCoord(5, 5), new LeptonCoord(5 * 256, 5 * 256),
            100, 100, m, false, EntityId.None, false, 1, 1);

}
