// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Vision;

namespace Rts.Sim.Tests.Vision;

public class VisionStateTests
{
    private static int CountVisible(VisionState v, byte player)
    {
        int n = 0;
        for (int y = 0; y < v.Height; y++)
            for (int x = 0; x < v.Width; x++)
                if (v.IsVisible(player, new CellCoord(x, y))) n++;
        return n;
    }

    private static int CountExplored(VisionState v, byte player)
    {
        int n = 0;
        for (int y = 0; y < v.Height; y++)
            for (int x = 0; x < v.Width; x++)
                if (v.VisionAt(player, new CellCoord(x, y)) >= 1) n++;
        return n;
    }

    [Fact]
    public void Reveal_MarksVisibleAndExplored()
    {
        var v = new VisionState(32, 32, 2);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(10, 10), 2);

        Assert.Equal(2, v.VisionAt(0, new CellCoord(10, 10)));
        Assert.True(v.IsVisible(0, new CellCoord(12, 10)));
        Assert.Equal(0, v.VisionAt(0, new CellCoord(20, 20)));
        Assert.Equal(0, v.VisionAt(1, new CellCoord(10, 10)));
    }

    [Fact]
    public void Move_UpdatesIncrementally_OldCellsFallToFog()
    {
        var v = new VisionState(32, 32, 1);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(10, 10), 1);
        Assert.Equal(2, v.VisionAt(0, new CellCoord(9, 10)));

        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(11, 10), 1);

        Assert.Equal(1, v.VisionAt(0, new CellCoord(9, 10)));
        Assert.Equal(1, v.VisionAt(0, new CellCoord(9, 9)));
        Assert.Equal(2, v.VisionAt(0, new CellCoord(10, 10)));
        Assert.Equal(2, v.VisionAt(0, new CellCoord(12, 10)));
        Assert.Equal(0, v.VisionAt(0, new CellCoord(8, 10)));
    }

    [Fact]
    public void Move_KeepsCellsVisibleWhileAnotherRevealerCoversThem()
    {
        var v = new VisionState(32, 32, 1);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(10, 10), 1);
        v.Reveal(0, ActorKind.Infantry, 1, new CellCoord(10, 10), 1);

        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(13, 10), 1);
        Assert.Equal(2, v.VisionAt(0, new CellCoord(9, 10)));

        v.Conceal(ActorKind.Infantry, 1);
        Assert.Equal(1, v.VisionAt(0, new CellCoord(9, 10)));
        Assert.Equal(2, v.VisionAt(0, new CellCoord(12, 10)));
    }

    [Fact]
    public void Reveal_SameCellSameSight_IsNoOp()
    {
        var v = new VisionState(32, 32, 1);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(10, 10), 2);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(10, 10), 2);

        v.Conceal(ActorKind.Vehicle, 1);
        Assert.Equal(0, CountVisible(v, 0));
    }

    [Fact]
    public void Reveal_ChangingPlayer_MovesTheDiscToTheNewOwner()
    {
        var v = new VisionState(32, 32, 2);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(10, 10), 1);
        v.Reveal(1, ActorKind.Vehicle, 1, new CellCoord(10, 10), 1);

        Assert.Equal(0, CountVisible(v, 0));
        Assert.Equal(9, CountVisible(v, 1));
    }

    [Fact]
    public void Conceal_TurnsVisibleToFogAndIsIdempotent()
    {
        var v = new VisionState(32, 32, 1);
        v.Reveal(0, ActorKind.Building, 3, new CellCoord(5, 5), 2);
        v.Conceal(ActorKind.Building, 3);
        v.Conceal(ActorKind.Building, 3);

        Assert.Equal(0, CountVisible(v, 0));
        Assert.Equal(1, v.VisionAt(0, new CellCoord(5, 5)));
        Assert.True(CountExplored(v, 0) > 0);
    }

    [Theory]
    [InlineData(1, 9)]
    [InlineData(3, 37)]
    [InlineData(5, 97)]
    public void Disc_MatchesFormulaAndCellCount(int sight, int expectedCount)
    {
        const int size = 32;
        var v = new VisionState(size, size, 1);
        var centre = new CellCoord(16, 16);
        v.Reveal(0, ActorKind.Vehicle, 1, centre, sight);

        int limit = sight * sight + sight;
        int visible = 0;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int dx = x - centre.X, dy = y - centre.Y;
                bool inDisc = dx * dx + dy * dy <= limit;
                Assert.Equal(inDisc, v.IsVisible(0, new CellCoord(x, y)));
                if (inDisc) visible++;
            }
        }
        Assert.Equal(expectedCount, visible);
    }

    [Fact]
    public void Disc_ClipsAtMapCorner()
    {
        var v = new VisionState(32, 32, 1);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(0, 0), 3);
        Assert.Equal(13, CountVisible(v, 0));

        var far = new VisionState(32, 32, 1);
        far.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(31, 31), 3);
        Assert.Equal(13, CountVisible(far, 0));
    }

    [Fact]
    public void Disc_CentreOffMap_IsClipped()
    {
        var v = new VisionState(32, 32, 1);
        v.Reveal(0, ActorKind.Vehicle, 1, new CellCoord(-1, 5), 1);

        Assert.Equal(3, CountVisible(v, 0));
        Assert.Equal(2, v.VisionAt(0, new CellCoord(0, 5)));
        Assert.Equal(0, v.VisionAt(0, new CellCoord(-5, 5)));
    }

    [Fact]
    public void ExploreAll_MarksEverythingFogNotVisible()
    {
        var v = new VisionState(16, 16, 2);
        v.ExploreAll(1);

        Assert.Equal(0, CountVisible(v, 1));
        Assert.Equal(16 * 16, CountExplored(v, 1));
        Assert.Equal(0, CountExplored(v, 0));
    }

    [Fact]
    public void FillVision_MatchesVisionAtForEveryCell()
    {
        var v = new VisionState(20, 12, 1);
        v.Reveal(0, ActorKind.Infantry, 7, new CellCoord(4, 4), 2);
        v.Reveal(0, ActorKind.Vehicle, 9, new CellCoord(12, 8), 3);
        v.Conceal(ActorKind.Infantry, 7);

        var dest = new byte[20 * 12];
        v.FillVision(0, dest);
        for (int y = 0; y < 12; y++)
            for (int x = 0; x < 20; x++)
                Assert.Equal(v.VisionAt(0, new CellCoord(x, y)), dest[y * 20 + x]);
    }

    [Fact]
    public void Ghosts_SetForgetAndKeepAscendingOrder()
    {
        var v = new VisionState(16, 16, 2);
        var a = new BuildingGhost(1, 4, new CellCoord(3, 3), 2, 2, 700);
        var b = new BuildingGhost(1, 6, new CellCoord(9, 1), 3, 3, 400);
        var c = new BuildingGhost(1, 2, new CellCoord(0, 12), 1, 1, 100);

        v.RecordBuildingSeen(0, 300, a);
        v.RecordBuildingSeen(0, 5, b);
        v.RecordBuildingSeen(0, 2, c);
        v.RecordBuildingSeen(0, 5, b with { Health = 350 });

        var ghosts = v.GhostsFor(0);
        Assert.Equal(new[] { 2, 5, 300 }, ghosts.Select(g => g.Index).ToArray());
        Assert.Equal(350, ghosts[1].Ghost.Health);
        Assert.Empty(v.GhostsFor(1));

        v.ForgetBuilding(0, 5);
        v.ForgetBuilding(0, 5);
        Assert.Equal(new[] { 2, 300 }, v.GhostsFor(0).Select(g => g.Index).ToArray());
    }

    [Fact]
    public void Ghosts_RejectOutOfRangeBuildingIndex()
    {
        var v = new VisionState(8, 8, 1);
        var g = new BuildingGhost(0, 0, default, 1, 1, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => v.RecordBuildingSeen(0, VisionState.GhostCapacity, g));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.ForgetBuilding(0, -1));
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        var v = new VisionState(8, 8, 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => v.Reveal(0, ActorKind.Vehicle, 1, default, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.Reveal(0, ActorKind.Vehicle, 1, default, VisionState.MaxSightCells + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.Reveal(1, ActorKind.Vehicle, 1, default, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.Reveal(0, ActorKind.Building, 2048, default, 1));
        Assert.Throws<ArgumentException>(() => v.FillVision(0, new byte[63]));
    }

    [Fact]
    public void WriteRead_RoundTripsEverything()
    {
        var rng = new SimRandom(4242UL);
        var v = new VisionState(40, 30, 3, 64);
        for (int step = 0; step < 300; step++)
        {
            byte p = (byte)rng.Next(3);
            var kind = (ActorKind)rng.Next(3);
            int index = 1 + rng.Next(60);
            var cell = new CellCoord(rng.Next(44) - 2, rng.Next(34) - 2);
            if (rng.Next(4) == 0) v.Conceal(kind, index);
            else v.Reveal(p, kind, index, cell, rng.Next(5));
        }
        for (int i = 0; i < 40; i++)
        {
            byte viewer = (byte)rng.Next(3);
            v.RecordBuildingSeen(viewer, rng.Next(VisionState.GhostCapacity),
                new BuildingGhost((byte)rng.Next(3), rng.Next(10), new CellCoord(rng.Next(40), rng.Next(30)),
                    1 + rng.Next(3), 1 + rng.Next(3), rng.Next(1000)));
        }
        v.ForgetBuilding(0, 3);
        v.ExploreAll(2);

        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) v.Write(w);
        byte[] bytes = ms.ToArray();

        var loaded = new VisionState(40, 30, 3, 64);
        using (var r = new BinaryReader(new MemoryStream(bytes), System.Text.Encoding.UTF8, leaveOpen: false))
            loaded.Read(r);

        var a = new byte[40 * 30];
        var b = new byte[40 * 30];
        for (byte p = 0; p < 3; p++)
        {
            v.FillVision(p, a);
            loaded.FillVision(p, b);
            Assert.True(a.AsSpan().SequenceEqual(b));
            Assert.Equal(v.GhostsFor(p), loaded.GhostsFor(p));
        }

        var ms2 = new MemoryStream();
        using (var w2 = new BinaryWriter(ms2, System.Text.Encoding.UTF8, leaveOpen: true)) loaded.Write(w2);
        Assert.True(bytes.AsSpan().SequenceEqual(ms2.ToArray()));
    }

    [Fact]
    public void Read_RejectsDimensionMismatch()
    {
        var src = new VisionState(16, 16, 2);
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true)) src.Write(w);
        ms.Position = 0;

        var other = new VisionState(16, 8, 2);
        using var r = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        Assert.Throws<InvalidDataException>(() => other.Read(r));
    }
}
