using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>Handler for one mission kind: runs one step for the actor at <c>i</c> and returns ticks to sleep (&gt;= 1).</summary>
internal delegate int MissionHandler(Simulation sim, MobileStore store, ActorKind kind, int i);

/// <summary>Static handler table indexed by <see cref="MissionKind"/>; no reflection, fully deterministic.</summary>
internal static class MissionHandlers
{
    public const int GuardSleepTicks = 15;
    /// <summary>Facing difference (of 256) above which a vehicle stops to turn: one 16-facing step.</summary>
    public const int TurnThreshold = 256 / 16;

    private static readonly MissionHandler[] Table = BuildTable();

    private static MissionHandler[] BuildTable()
    {
        var t = new MissionHandler[Enum.GetValues<MissionKind>().Length];
        MissionHandler idle = Idle;
        for (int k = 0; k < t.Length; k++) t[k] = idle;
        t[(int)MissionKind.Move] = HandleMove;
        t[(int)MissionKind.Stop] = HandleStop;
        return t;
    }

    public static int Handle(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        int k = (int)store.Mission[i];
        int sleep = k < Table.Length ? Table[k](sim, store, kind, i) : GuardSleepTicks;
        return sleep < 1 ? 1 : sleep;
    }

    // Guard/Sleep/None/not-yet-implemented kinds: idle (target acquisition arrives with M1-09).
    private static int Idle(Simulation sim, MobileStore store, ActorKind kind, int i) => GuardSleepTicks;

    private static int HandleStop(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        store.Destination[i] = store.Position[i];
        store.NextMission[i] = MissionKind.Guard;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        return 1;
    }

    private static int HandleMove(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        LeptonCoord pos = store.Position[i], dest = store.Destination[i];
        int dx = dest.X - pos.X, dy = dest.Y - pos.Y;
        if (dx == 0 && dy == 0) return Arrive(store, i);

        byte desired = Facing.FromDelta(dx, dy);
        int speed;
        if (kind == ActorKind.Infantry)
        {
            speed = sim.Setup.Rules.Infantry[store.TypeIndex[i]].Speed;
            store.Facing[i] = desired;                       // infantry turn instantly
            store.Status[i] = MissionStatus.Driving;
        }
        else
        {
            var type = sim.Setup.Rules.Vehicles[store.TypeIndex[i]];
            speed = type.Speed;
            byte cur = store.Facing[i];
            int diff = Math.Abs(Facing.Difference(cur, desired));
            // D-042: a large error starts a turn in place that runs until aligned (the aligning tick does not
            // move); small errors while driving are corrected in stride.
            if (store.Status[i] == MissionStatus.Turning || diff > TurnThreshold)
            {
                byte next = Facing.TurnToward(cur, desired, type.TurnRate);
                store.Facing[i] = next;
                store.Status[i] = next == desired ? MissionStatus.Driving : MissionStatus.Turning;
                return 1;
            }
            store.Status[i] = MissionStatus.Driving;
            if (diff != 0) store.Facing[i] = Facing.TurnToward(cur, desired, type.TurnRate);
        }

        if (speed <= 0) return 1;
        int ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (Math.Max(ax, ay) <= speed)
        {
            store.Position[i] = dest;
            return Arrive(store, i);
        }
        int sx, sy;
        if (ax >= ay) { sx = speed; sy = (int)((long)ay * speed / ax); }
        else { sy = speed; sx = (int)((long)ax * speed / ay); }
        store.Position[i] = new LeptonCoord(pos.X + (dx < 0 ? -sx : sx), pos.Y + (dy < 0 ? -sy : sy));
        return 1;
    }

    private static int Arrive(MobileStore store, int i)
    {
        store.NextMission[i] = MissionKind.Guard;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        return 1;
    }
}
