using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>Move and Stop mission handlers. OWNER: M1-08 (movement and locomotion).</summary>
internal static class MoveMission
{
    public static int HandleStop(Simulation sim, MobileStore store, ActorKind kind, int i)
    {
        store.Destination[i] = store.Position[i];
        store.NextMission[i] = MissionKind.Guard;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        return 1;
    }

    public static int HandleMove(Simulation sim, MobileStore store, ActorKind kind, int i)
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
            if (store.Status[i] == MissionStatus.Turning || diff > MissionHandlers.TurnThreshold)
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

    internal static int Arrive(MobileStore store, int i)
    {
        store.NextMission[i] = MissionKind.Guard;
        store.NextCell[i] = default;
        store.NextTarget[i] = default;
        return 1;
    }

    /// <summary>
    /// CONTRACT for other missions (M1-09 Attack/AttackMove/Guard chase): advance actor <paramref name="i"/> one tick
    /// toward <paramref name="cell"/> using normal movement rules, WITHOUT finishing the current mission or queueing
    /// Guard on arrival. Returns ticks to sleep (≥1); returns 1 and does nothing once the actor stands in that cell.
    /// Lead stub: straight line, like the Move handler. M1-08 replaces the body with path following; keep the signature.
    /// </summary>
    public static int StepToward(Simulation sim, MobileStore store, ActorKind kind, int i, CellCoord cell)
    {
        LeptonCoord target = LeptonCoord.CenterOf(cell);
        if (store.Position[i] == target) return 1;
        store.Destination[i] = target;
        MissionKind keep = store.NextMission[i];
        int sleep = HandleMove(sim, store, kind, i);
        store.NextMission[i] = keep; // HandleMove queues Guard on arrival; StepToward must not change the mission
        return sleep;
    }
}
