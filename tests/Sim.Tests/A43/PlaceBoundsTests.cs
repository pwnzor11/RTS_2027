// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Tests.Determinism;
using Rts.Sim.World;
using Xunit;

namespace Rts.Sim.Tests.A43;

/// <summary>A43 SIMCORE-01: a huge top-left cell must be refused as off-map, never wrap past the bounds test.</summary>
public sealed class PlaceBoundsTests
{
    [Theory]
    [InlineData(int.MaxValue, 0)]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MaxValue - 1, int.MaxValue - 1)]
    [InlineData(int.MinValue, 0)]
    public void CanPlace_RefusesHugeCells(int x, int y)
    {
        var sim = ScriptedGame.NewSim(ScriptedGame.Setup());
        for (int type = 0; type < sim.Setup.Rules.Buildings.Count; type++)
            Assert.Equal(PlacementResult.OutOfMap, sim.CanPlace(0, type, new CellCoord(x, y)));
    }
}
