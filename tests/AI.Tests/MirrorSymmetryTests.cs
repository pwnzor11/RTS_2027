// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.AI;
using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;
using Xunit.Abstractions;

namespace AI.Tests;

/// <summary>M2-01: a point-mirrored setup must move the same from either start corner (D-046 corner advantage).</summary>
public class MirrorSymmetryTests(ITestOutputHelper output)
{
    private static MapData Flat(MapData m)
    {
        int n = m.Width * m.Height;
        return new MapData("flat", m.Theater, m.Width, m.Height, new ushort[n], new LandType[n], new byte[n],
            new OverlayKind[n], new byte[n], m.StartPositions);
    }

    /// <summary>First tick at which an actor of start 0 is not the 180-degree mirror of its twin at start 1, or -1.</summary>
    private int FirstDivergence(MapData map, ulong seed, int ticks, bool swapStarts)
    {
        int w = map.Width * 256, h = map.Height * 256;
        int found = -1;
        AiMatchRunner.PlayScripted(map, TestWorld.Rules.Sim, TestWorld.Rules.RulesHash, seed, ticks, swapStarts, (sim, armies) =>
        {
            if (found >= 0) return;
            for (int k = 0; k < armies[0].Count && found < 0; k++)
            {
                var (a, b) = (armies[0][k], armies[1][k]);
                MobileStoreView sa = View(sim, a.Kind), sb = View(sim, b.Kind);
                var pa = sa.Pos[a.Id.Index]; var pb = sb.Pos[b.Id.Index];
                bool mirrored = pa.X == w - pb.X && pa.Y == h - pb.Y
                    && sa.Facing[a.Id.Index] == (byte)(sb.Facing[b.Id.Index] + 128);
                if (!mirrored)
                {
                    found = sim.Tick;
                    output.WriteLine($"tick {sim.Tick} k{k} {a.Kind}: {pa} vs {pb}, facing {sa.Facing[a.Id.Index]} vs {sb.Facing[b.Id.Index]}");
                }
            }
        });
        return found;
    }

    private readonly record struct MobileStoreView(LeptonCoord[] Pos, byte[] Facing);
    private static MobileStoreView View(Simulation sim, ActorKind kind) =>
        kind == ActorKind.Vehicle ? new(sim.Units.Position, sim.Units.Facing) : new(sim.Infantry.Position, sim.Infantry.Facing);

    [Theory]
    [InlineData(1UL, true, false)]
    [InlineData(1UL, false, false)]
    [InlineData(2UL, false, true)]
    public void MirroredArmiesMoveAsMirrorImagesBeforeContact(ulong seed, bool flat, bool swapStarts)
    {
        MapData map = Rts.Data.MapGenerator.Generate(64, seed);
        if (flat) map = Flat(map);
        // The armies start 80 cells apart and first shots fall after about 260 ticks (flat map); until then every deviation is
        // geometric (facing, path tie-breaks, sub-slots, path batching) and must not exist.
        Assert.Equal(-1, FirstDivergence(map, seed, 200, swapStarts));
    }

    [Fact]
    public void GeneratedMapIsPointSymmetric()
    {
        MapData m = Rts.Data.MapGenerator.Generate(64, 3);
        int n = m.Width * m.Height;
        for (int i = 0; i < n; i++)
        {
            Assert.Equal(m.Land[i], m.Land[n - 1 - i]);
            Assert.Equal(m.Overlay[i], m.Overlay[n - 1 - i]);
        }
    }
}
