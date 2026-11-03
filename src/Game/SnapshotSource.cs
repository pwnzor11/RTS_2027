using System.Diagnostics;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game;

/// <summary>Supplies the snapshot the renderer draws each frame. The renderer never calls the simulation.</summary>
public interface ISnapshotSource
{
    /// <summary>Latest snapshot. Never null.</summary>
    SimSnapshot Current { get; }

    /// <summary>Advances the source to the current wall-clock time.</summary>
    void Update();
}

/// <summary>
/// Fabricated 32x32 two-player map with six units circling the centre. Integer-only movement on a
/// 20 Hz accumulator; a stand-in until the simulation is wired in.
/// </summary>
public sealed class DemoSnapshotSource : ISnapshotSource
{
    private const int MapCells = 32;
    private const int TickMs = 1000 / SimConstants.TicksPerSecond; // 50 ms at 20 Hz
    private const int MaxAccumulatedMs = 250;                       // avoid a burst of ticks after a stall
    private const int OrbitRadiusLeptons = 10 * SimConstants.LeptonsPerCell;
    private const int CentreLeptons = (MapCells / 2) * SimConstants.LeptonsPerCell;
    private const int HalfTurnIndex = 128;
    private const int QuarterTurnIndex = 64;

    // Units 0-2 belong to player 0 and orbit clockwise; units 3-5 belong to player 1, opposite side, counter-clockwise.
    private static readonly LandType[] _land = new LandType[MapCells * MapCells]; // all Clear
    private static readonly int[] Phase = { 0, 85, 170, 128, 213, 42 };

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _lastMs;
    private int _accumulatedMs;
    private int _tick;
    private SimSnapshot _current;

    /// <summary>Creates the source with the first snapshot already built at tick 0.</summary>
    public DemoSnapshotSource()
    {
        _current = Build();
    }

    /// <inheritdoc />
    public SimSnapshot Current => _current;

    /// <inheritdoc />
    public void Update()
    {
        long nowMs = _clock.ElapsedMilliseconds;
        _accumulatedMs = Math.Min(_accumulatedMs + (int)(nowMs - _lastMs), MaxAccumulatedMs);
        _lastMs = nowMs;

        bool advanced = false;
        while (_accumulatedMs >= TickMs)
        {
            _accumulatedMs -= TickMs;
            _tick++;
            advanced = true;
        }

        if (advanced)
        {
            _current = Build();
        }
    }

    private SimSnapshot Build()
    {
        var units = new UnitView[Phase.Length];
        for (int i = 0; i < Phase.Length; i++)
        {
            byte player = (byte)(i < 3 ? 0 : 1);
            int direction = player == 0 ? 1 : -1;
            // One lap is 256 angle steps; at 5/4 steps per tick that is about 205 ticks (roughly 10 s).
            int angle = (Phase[i] + direction * (_tick * 5 / 4)) & 0xFF;

            int x = CentreLeptons + OrbitRadiusLeptons * SinQ10(angle) / 1024;
            int y = CentreLeptons - OrbitRadiusLeptons * SinQ10(angle + QuarterTurnIndex) / 1024;

            // Tangent to the orbit: clockwise travel faces angle + 90 degrees, counter-clockwise faces angle - 90.
            int facing = (angle + (direction > 0 ? QuarterTurnIndex : -QuarterTurnIndex)) & 0xFF;

            units[i] = new UnitView(
                new EntityId((ushort)(i + 1), 1),
                UnitKind.Vehicle,
                player,
                0,
                new LeptonCoord(x, y),
                (byte)facing,
                100);
        }

        return new SimSnapshot(_tick, MapCells, MapCells, units, _land, 0UL);
    }

    /// <summary>
    /// Integer sine scaled by 1024 for an angle index 0..255 (256 = full turn), from Bhaskara I's rational
    /// approximation. Accurate to about 1% over the circle, which is plenty for a placeholder.
    /// </summary>
    private static int SinQ10(int index)
    {
        index &= 0xFF;
        int sign = 1;
        if (index >= HalfTurnIndex)
        {
            index -= HalfTurnIndex;
            sign = -1;
        }

        int degrees = index * 180 / HalfTurnIndex;   // 0..179
        int product = degrees * (180 - degrees);      // 0..8100
        return sign * (4 * product * 1024 / (40500 - product));
    }
}
