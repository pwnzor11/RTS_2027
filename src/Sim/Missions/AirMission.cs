// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim.Missions;

/// <summary>
/// Flight for <see cref="Locomotion.Air"/> vehicles (M3-06, design 05 §4): a straight line to the destination at the type's speed
/// (times the aura multiplier), ignoring terrain, buildings and every other unit. Air units hold no map cell
/// (<see cref="Movement.Occupancy.Claim"/> skips them), so they neither block nor are blocked. MoveMission hands over to this at the top
/// of HandleMove and StepToward.
/// </summary>
internal static class AirMission
{
    /// <summary>True if the actor is a vehicle with air locomotion.</summary>
    public static bool IsAir(Simulation sim, ActorKind kind, MobileStore store, int i) =>
        kind == ActorKind.Vehicle && sim.RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]].Locomotion == Locomotion.Air;

    public static int HandleMove(Simulation sim, MobileStore store, int i)
    {
        if (store.Status[i] == MissionStatus.Init)
        {
            if (store.PathId[i] != -1) MoveMission.DropRequest(sim, store, i);   // -1: the command's batch is still being set up
            store.Status[i] = MissionStatus.Waiting;
        }
        if (store.PathId[i] == -1) return 1;
        if (store.PathId[i] > 0) MoveMission.DropRequest(sim, store, i);   // the batch asked for a ground route nobody needs
        store.PathActive[i] = false; store.PathCells[i] = null;
        if (Fly(sim, store, i, store.Destination[i])) return MoveMission.Arrive(store, i);
        return 1;
    }

    public static int StepToward(Simulation sim, MobileStore store, int i, CellCoord cell)
    {
        Fly(sim, store, i, LeptonCoord.CenterOf(cell));
        return 1;
    }

    /// <summary>One tick of flight toward the point; true once it is reached.</summary>
    private static bool Fly(Simulation sim, MobileStore store, int i, LeptonCoord target)
    {
        VehicleType t = sim.RulesOf(store.Player[i]).Vehicles[store.TypeIndex[i]];
        LeptonCoord pos = store.Position[i];
        if (pos == target) return true;
        byte desired = Facing.FromDelta(target.X - pos.X, target.Y - pos.Y);
        store.Facing[i] = t.TurnRate > 0 ? Facing.TurnToward(store.Facing[i], desired, t.TurnRate) : desired;
        store.Status[i] = MissionStatus.Driving;
        int speed = Math.Max(1, (int)((long)t.Speed * sim.MoveSpeedPermille(ActorKind.Vehicle, i) / 1000));
        int dx = target.X - pos.X, dy = target.Y - pos.Y, ax = Math.Abs(dx), ay = Math.Abs(dy);
        if (Math.Max(ax, ay) <= speed) store.Position[i] = target;
        else
        {
            int sx = ax >= ay ? speed : (int)((long)ax * speed / ay);
            int sy = ax >= ay ? (int)((long)ay * speed / ax) : speed;
            store.Position[i] = new LeptonCoord(pos.X + (dx < 0 ? -sx : sx), pos.Y + (dy < 0 ? -sy : sy));
        }
        return store.Position[i] == target;
    }
}
