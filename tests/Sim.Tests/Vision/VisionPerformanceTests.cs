using System.Diagnostics;
using Rts.Sim.Core;
using Rts.Sim.Vision;
using Xunit.Abstractions;

namespace Rts.Sim.Tests.Vision;

/// <summary>Vision throughput. Timing is logged, never asserted: wall-clock bounds are flaky on CI and say nothing about trends.</summary>
public class VisionPerformanceTests(ITestOutputHelper output)
{
    private const int Width = 128;
    private const int Height = 128;
    private const int ActorCount = 400;
    private const int Steps = 200;
    private const int Sight = 4;

    // Seeded random walk: every actor moves by -1..1 per axis each step and reveals its disc.
    // Returns the state after the last step plus the final positions.
    private static (VisionState State, CellCoord[] Positions) RandomWalk(ulong seed)
    {
        var rng = new SimRandom(seed);
        var state = new VisionState(Width, Height, 2);
        var pos = new CellCoord[ActorCount + 1];
        for (int i = 1; i <= ActorCount; i++)
            pos[i] = new CellCoord(rng.Next(Width), rng.Next(Height));

        for (int step = 0; step < Steps; step++)
        {
            for (int i = 1; i <= ActorCount; i++)
            {
                int x = Math.Clamp(pos[i].X + rng.Next(3) - 1, 0, Width - 1);
                int y = Math.Clamp(pos[i].Y + rng.Next(3) - 1, 0, Height - 1);
                pos[i] = new CellCoord(x, y);
                state.Reveal((byte)(i & 1), ActorKind.Vehicle, i, pos[i], Sight);
            }
        }
        return (state, pos);
    }

    private static byte[] AllLayers(VisionState state)
    {
        var all = new byte[2 * Width * Height];
        var layer = new byte[Width * Height];
        for (byte p = 0; p < 2; p++)
        {
            state.FillVision(p, layer);
            layer.CopyTo(all, p * Width * Height);
        }
        return all;
    }

    [Fact]
    public void RandomWalk_IsDeterministicForSameSeed()
    {
        byte[] first = AllLayers(RandomWalk(20260311UL).State);
        byte[] second = AllLayers(RandomWalk(20260311UL).State);
        Assert.True(first.AsSpan().SequenceEqual(second));
    }

    [Fact]
    public void RandomWalk_IncrementalMatchesRebuildFromScratch()
    {
        var (state, pos) = RandomWalk(777UL);

        var fresh = new VisionState(Width, Height, 2);
        for (int i = 1; i <= ActorCount; i++)
            fresh.Reveal((byte)(i & 1), ActorKind.Vehicle, i, pos[i], Sight);

        var a = new byte[Width * Height];
        var b = new byte[Width * Height];
        for (byte p = 0; p < 2; p++)
        {
            state.FillVision(p, a);
            fresh.FillVision(p, b);
            for (int i = 0; i < a.Length; i++)
                Assert.Equal(b[i] == 2, a[i] == 2);
        }
    }

    [Fact]
    public void RandomWalk_400ActorsOn128x128For200Steps_LogsTicksPerSecond()
    {
        var sw = Stopwatch.StartNew();
        RandomWalk(12345UL);
        sw.Stop();
        double ticksPerSecond = Steps / Math.Max(sw.Elapsed.TotalSeconds, 1e-9);
        output.WriteLine($"Vision random walk: {ActorCount} actors, {Width}x{Height}, {Steps} ticks in {sw.ElapsedMilliseconds} ms = {ticksPerSecond:F0} ticks/s");
    }
}
