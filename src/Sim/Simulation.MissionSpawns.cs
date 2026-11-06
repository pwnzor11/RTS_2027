// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.Sim;

// M8-04: thin public wrappers over existing transport, landing, rig and veterancy rules, for the mission host (Rts.Scripting), which
// cannot see internals. Nothing here adds a rule: each wrapper runs the code a player command would run, from the scripting entry
// point (setup phase or inside Step, like SpawnVehicle and AssignAttackMove). No new commands, no new state.
public sealed partial class Simulation
{
    /// <summary>
    /// Scripting entry: gives a live vehicle or soldier the veterancy rank <paramref name="rank"/> (1..3) by raising its kill value to
    /// the rank's threshold (scaled by the type's <c>vet_threshold_permille</c>, as <see cref="RankOf"/> reads it). Never lowers a rank.
    /// Returns the rank the actor has afterwards; 0 when veterancy is off, the type cannot rank, or the actor is gone.
    /// </summary>
    public int ScriptSetRank(ActorKind kind, EntityId id, int rank)
    {
        RequireSetupPhase();
        VeterancyRules vr = VetRules;
        if (!vr.Enabled || kind > ActorKind.Infantry || rank < 1 || rank > VeterancyRules.Ranks || !SlotsOf(kind).IsAlive(id)) return 0;
        int i = id.Index;
        if (!CanRankAt(kind, i)) return 0;
        SimRules r = RulesOf(PlayerAt(kind, i));
        int scale = kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].VetThresholdPermille : r.Infantry[_infantry.TypeIndex[i]].VetThresholdPermille;
        int need = (int)Math.Min(int.MaxValue, Math.Max(1L, (long)vr.Thresholds[rank - 1] * scale / 1000));
        int[] kills = kind == ActorKind.Vehicle ? _veterancy.VehicleKillValue : _veterancy.InfantryKillValue;
        if (kills[i] < need) kills[i] = need;
        return RankOf(kind, i);
    }

    /// <summary>Scripting entry: puts a soldier (or, on a carrier with vehicle slots, a ground vehicle) aboard an own transport at once,
    /// exactly as boarding next to it would. False when the transport has no room or the passenger cannot ride it.</summary>
    public bool ScriptBoard(ActorRef passenger, EntityId transport)
    {
        RequireSetupPhase();
        if (!_units.Slots.IsAlive(transport)) return false;
        return passenger.Kind switch
        {
            ActorKind.Infantry => _infantry.Slots.IsAlive(passenger.Id) && BoardNow(passenger.Id.Index, transport),
            ActorKind.Vehicle => _units.Slots.IsAlive(passenger.Id) && BoardVehicleNow(passenger.Id.Index, transport),
            _ => false,
        };
    }

    /// <summary>Scripting entry: the transport puts everyone aboard onto the free cells around it (the Deploy order of a transport).
    /// True when nobody is left aboard afterwards.</summary>
    public bool ScriptUnload(EntityId transport)
    {
        RequireSetupPhase();
        if (!_units.Slots.IsAlive(transport)) return false;
        ApplyUnload(new Command(Tick, _units.Player[transport.Index], 0, CommandKind.Unload, transport, default, default, 0));
        return PassengerCount(transport) == 0;
    }

    /// <summary>Scripting entry: orders a landing craft (a transport with <c>unload_ticks</c>) to sail to a water cell beside the shore cell
    /// and put its passengers ashore (the Unload order). False, and nothing queued, when the vehicle is not a landing transport or the
    /// order is refused (shore cell not standable, nobody aboard, no reachable sea cell beside it).</summary>
    public bool ScriptLandingUnload(EntityId transport, CellCoord shore)
    {
        RequireSetupPhase();
        if (!_units.Slots.IsAlive(transport)) return false;
        int i = transport.Index;
        VehicleType vt = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]];
        if (!IsLandingTransport(vt)) return false;
        ApplyLandingUnload(new Command(Tick, _units.Player[i], 0, CommandKind.Unload, transport, default, shore, (int)ActorKind.Vehicle), vt);
        return _units.NextMission[i] == MissionKind.Unload || _units.Mission[i] == MissionKind.Unload;
    }

    /// <summary>Scripting entry: orders a packed rig to drive to <paramref name="topLeft"/> and unpack there (the Pack command on a packed
    /// rig). False, and nothing queued, when the vehicle is not an own packed rig or the footprint is not a legal site now.</summary>
    public bool ScriptUnpackRig(EntityId rig, CellCoord topLeft)
    {
        RequireSetupPhase();
        if (!_units.Slots.IsAlive(rig)) return false;
        int i = rig.Index;
        if (_units.Health[i] <= 0 || !_rigs.IsPackedRig(i) || !InMap(topLeft)) return false;
        int into = UnpackTypeOf(i);
        if (into < 0 || !UnpackSiteOk(_units.Player[i], into, topLeft, i)) return false;
        ApplyPack(new Command(Tick, _units.Player[i], 0, CommandKind.Pack, rig, default, topLeft, 0));
        return _units.NextMission[i] == MissionKind.Pack;
    }
}
