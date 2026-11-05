// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// M6-05 (OWNER: M6-05 package; hooks placed by the lead in session 9). Raiders: plunder, withdraw, spoils (10_m6_plan.md §3.2).
// Hooks: ApplyEnter -> TryApplyRaid first (after the subject check); ApplyPendingDamage -> OnSpoilsLethalHit after OnLethalHit;
// MissionKind.Withdraw -> Missions/WithdrawMission.cs.
//
// Raid walk: the raider gets MissionKind.Capture with Target = the building and CombatData.AttackTarget = that building as the marker
// (the same marker M4-03 uses for a garrison walk; a raid is told apart by the type being a Raider and the target an enemy store).
// Plunder: the thief's share goes straight to Cash (uncapped, like a refund), not to Stored, so a full silo does not waste it.
public sealed partial class Simulation
{
    /// <summary>Withdraw timeout in ticks (tune): a raider that has not reached home by then simply stops withdrawing.</summary>
    internal const int WithdrawTimeoutTicks = 600;

    /// <summary>True if building slot <paramref name="b"/> holds ash a raider can steal: a live refinery or silo (storage) of an enemy.</summary>
    internal bool IsPlunderTarget(byte raiderPlayer, int b)
    {
        if (!_buildings.Slots.IsIndexAlive(b) || _buildings.Health[b] <= 0) return false;
        if (RelationOf(raiderPlayer, _buildings.Player[b]) != Relation.Enemy) return false;
        BuildingType bt = BuildingTypeAt(b);
        return bt.Refinery || bt.Storage > 0;
    }

    /// <summary>True if the mobile actor at slot <paramref name="i"/> is of a raider type.</summary>
    internal bool IsRaiderUnit(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].Raider
            : kind == ActorKind.Infantry && r.Infantry[_infantry.TypeIndex[i]].Raider;
    }

    /// <summary>Enter from a unit with raider = true on an enemy Silo/Refinery: start the plunder. True = handled.</summary>
    private bool TryApplyRaid(Command c, ActorRef subject)
    {
        if (subject.Kind != ActorKind.Vehicle && subject.Kind != ActorKind.Infantry) return false;
        if (((c.Arg >> 8) & 0xFF) != (int)ActorKind.Building || !_buildings.Slots.IsAlive(c.Target)) return false;
        int i = subject.Id.Index;
        if (!IsRaiderUnit(subject.Kind, i)) return false;
        if (!IsPlunderTarget(c.Player, c.Target.Index)) return false;
        if (subject.Kind == ActorKind.Infantry && _tunnels.InfantryLayer[i] != ActorLayer.Surface) return false;
        MobileStore store = subject.Kind == ActorKind.Vehicle ? _units : _infantry;
        SetWithdrawFlag(subject.Kind, i, false);
        _combat.At(subject.Kind, i).AttackTarget = new ActorRef(ActorKind.Building, c.Target);   // raid marker
        Queue(store, i, MissionKind.Capture, default, c.Target);
        return true;
    }

    /// <summary>The raider at slot <paramref name="i"/> has reached the footprint of <paramref name="building"/>: steal the ash, then be
    /// consumed (one-use) or withdraw. False when the building is no longer a valid target (the walk then ends).</summary>
    internal bool PlunderNow(ActorKind kind, int i, EntityId building)
    {
        if (!_buildings.Slots.IsAlive(building)) return false;
        byte thief = PlayerAt(kind, i);
        int b = building.Index;
        if (!IsPlunderTarget(thief, b)) return false;
        byte victim = _buildings.Player[b];
        EconomyRules eco = RulesOf(thief).Economy;
        long amount = (long)_players.Stored[victim] * Math.Max(0, eco.PlunderPercent) / 100;
        amount = amount * UpgradePermille(thief, u => u.PlunderPermille) / 1000;
        if (eco.PlunderCap > 0 && amount > eco.PlunderCap) amount = eco.PlunderCap;
        int take = (int)Math.Min(amount, _players.Stored[victim]);
        if (take > 0)
        {
            _players.Stored[victim] -= take;
            _players.Cash[thief] += take;
            OnStatsHarvested(thief, take);
        }
        _combat.At(kind, i).AttackTarget = default;
        SimRules r = RulesOf(thief);
        bool oneUse = kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].RaiderOneUse : r.Infantry[_infantry.TypeIndex[i]].RaiderOneUse;
        if (oneUse) { RemoveMobileQuietly(kind, i); return true; }   // consumed: no wreck, no death weapon
        SetWithdrawFlag(kind, i, true);
        int deadline = Tick + WithdrawTimeoutTicks;
        AssignMission(new ActorRef(kind, IdAt(kind, i)), MissionKind.Withdraw, default,
            new EntityId((ushort)(deadline & 0xFFFF), (ushort)((deadline >> 16) & 0xFFFF)));   // the timeout rides in Target (no new state)
        return true;
    }

    internal void SetWithdrawFlag(ActorKind kind, int i, bool on)
    {
        if (kind == ActorKind.Vehicle) _effects.VehicleWithdraw[i] = (byte)(on ? 1 : 0);
        else if (kind == ActorKind.Infantry) _effects.InfantryWithdraw[i] = (byte)(on ? 1 : 0);
    }

    /// <summary>A hit from <paramref name="source"/> killed <paramref name="target"/>: pay spoils when the killer is a raider type and
    /// the victim a harvester. ASSUMED (PendingHit carries only the player): the source player's faction has any raider type.</summary>
    internal void OnSpoilsLethalHit(ActorRef target, byte source)
    {
        if (target.Kind != ActorKind.Vehicle || source >= _players.Count || _players.IsNeutral(source)) return;
        int v = target.Id.Index;
        byte owner = _units.Player[v];
        if (RelationOf(source, owner) != Relation.Enemy) return;
        int permille = RulesOf(source).Economy.SpoilsPermille;
        if (permille <= 0) return;
        VehicleType vt = RulesOf(owner).Vehicles[_units.TypeIndex[v]];
        if (vt.HarvestCapacity <= 0 || !HasRaiderType(source)) return;
        int pay = (int)((long)vt.Cost * permille / 1000);
        if (pay > 0) _players.Cash[source] += pay;
    }

    private bool HasRaiderType(byte player)
    {
        SimRules r = RulesOf(player);
        for (int k = 0; k < r.Vehicles.Count; k++) if (r.Vehicles[k].Raider) return true;
        for (int k = 0; k < r.Infantry.Count; k++) if (r.Infantry[k].Raider) return true;
        return false;
    }
}
