// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Effects;
using Rts.Sim.Rules;

namespace Rts.Sim;

// S44 M-05 Harpoon tether and M-06 Grapple Hooks wall breach. State: EffectsState v4 (Tether*, BuildingBreach*), hashed and saved.
//
// TETHER (M-05): a hit by a warhead with TetherTicks > 0 that leaves a vehicle or soldier alive tethers it for TetherTicks ticks: its
//  move speed is multiplied by TetherSpeedPermille / 1000 (Simulation.MoveSpeedPermille, on top of auras, storms and rank). A second hit
//  refreshes the timer to the longer of the two and keeps the stronger (lower) slow. Buildings are never tethered. There is no leash
//  (ASSUMED: design 06 only says "tethers (slows)"). Ticked down here, before RunEffects.
// BREACH (M-06): a Raider soldier (Hook Runner) of a player holding an upgrade with BreachTicks > 0 that stands next to an ENEMY wall
//  segment (8 neighbours) opens it: for BreachTicks ticks the segment is passable for the breacher's team (the same hook gates use:
//  GateOpenFor), then it closes. A segment with an actor on it stays open until the cell is free. After closing it cannot be breached
//  again for BreachCooldownTicks (stored as a negative count so no extra array is needed). Gates and non-wall buildings are not breached.
public sealed partial class Simulation
{
    /// <summary>A hit that did not kill (<paramref name="target"/> alive) landed with warhead <paramref name="warhead"/> (an index into the
    /// shooter player's rules): start or refresh the tether. Called from ApplyPendingDamage.</summary>
    internal void OnTetherHit(ActorKind kind, int i, int warhead, byte source)
    {
        if (warhead < 0 || kind == ActorKind.Building || source >= _players.Count) return;
        IReadOnlyList<WarheadType> list = RulesOf(source).Warheads;
        if (warhead >= list.Count) return;
        WarheadType wh = list[warhead];
        if (wh.TetherTicks <= 0 || HealthAt(kind, i) <= 0) return;
        int[] ticks = kind == ActorKind.Vehicle ? _effects.VehicleTetherTicks : _effects.InfantryTetherTicks;
        int[] permille = kind == ActorKind.Vehicle ? _effects.VehicleTetherPermille : _effects.InfantryTetherPermille;
        int p = Math.Clamp(wh.TetherSpeedPermille, 1, 1000);
        permille[i] = ticks[i] > 0 ? Math.Min(permille[i], p) : p;
        if (wh.TetherTicks > ticks[i]) ticks[i] = wh.TetherTicks;
    }

    /// <summary>Tether speed multiplier per mille of a mobile slot (1000 when not tethered).</summary>
    internal int TetherPermille(ActorKind kind, int i)
    {
        if (kind == ActorKind.Vehicle) return _effects.VehicleTetherTicks[i] > 0 ? _effects.VehicleTetherPermille[i] : 1000;
        if (kind == ActorKind.Infantry) return _effects.InfantryTetherTicks[i] > 0 ? _effects.InfantryTetherPermille[i] : 1000;
        return 1000;
    }

    /// <summary>True while the wall segment in building slot <paramref name="b"/> is breached open for <paramref name="player"/>'s team.</summary>
    internal bool BreachOpenFor(int b, byte player) =>
        b > 0 && b < _effects.BuildingBreach.Length && _effects.BuildingBreach[b] > 0 && IsFriendly(player, _effects.BuildingBreachOwner[b]);

    /// <summary>Per tick, before RunEffects: tick the tether and breach timers, then open walls next to breachers.</summary>
    private void RunTethers()
    {
        EffectsState e = _effects;
        TickTethers(e.VehicleTetherTicks, e.VehicleTetherPermille);
        TickTethers(e.InfantryTetherTicks, e.InfantryTetherPermille);
        for (int b = 1; b < e.BuildingBreach.Length; b++)
        {
            int t = e.BuildingBreach[b];
            if (t == 0) continue;
            if (!_buildings.Slots.IsIndexAlive(b)) { e.BuildingBreach[b] = 0; e.BuildingBreachOwner[b] = 0; continue; }
            if (t > 0)
            {
                if (t == 1 && ActorStandsOn(_buildings.Cell[b])) continue;   // someone is in the gap: hold it open
                if (t > 1) { e.BuildingBreach[b] = t - 1; continue; }
                int cooldown = BreachCooldownOf(e.BuildingBreachOwner[b]);
                e.BuildingBreach[b] = -cooldown;
                if (cooldown == 0) e.BuildingBreachOwner[b] = 0;
            }
            else
            {
                e.BuildingBreach[b] = t + 1;
                if (t + 1 == 0) e.BuildingBreachOwner[b] = 0;
            }
        }
        RunBreachers();
    }

    private static void TickTethers(int[] ticks, int[] permille)
    {
        for (int i = 1; i < ticks.Length; i++)
        {
            if (ticks[i] <= 0) { if (permille[i] != 0) permille[i] = 0; continue; }
            if (--ticks[i] == 0) permille[i] = 0;
        }
    }

    private int BreachTicksOf(byte player) => BreachFieldMax(player, u => u.BreachTicks);

    private int BreachCooldownOf(byte player) => BreachFieldMax(player, u => u.BreachCooldownTicks);

    private int BreachFieldMax(byte player, Func<UpgradeType, int> field)
    {
        if (player >= _players.Count) return 0;
        IReadOnlyList<UpgradeType> ups = RulesOf(player).Upgrades;
        int best = 0;
        for (int u = 0; u < ups.Count; u++)
            if (field(ups[u]) > best && HasUpgrade(player, u) && ups[u].BreachTicks > 0) best = field(ups[u]);
        return best;
    }

    private static readonly (int dx, int dy)[] Around = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    private void RunBreachers()
    {
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _infantry.Health[i] <= 0) continue;
            byte p = _infantry.Player[i];
            if (p >= _players.Count || _players.IsNeutral(p) || !RulesOf(p).Infantry[_infantry.TypeIndex[i]].Raider) continue;
            if (_tunnels.InfantryLayer[i] != ActorLayer.Surface) continue;
            int open = BreachTicksOf(p);
            if (open <= 0) continue;
            CellCoord c = _infantry.SlotCell[i];
            for (int n = 0; n < Around.Length; n++)
            {
                var nc = new CellCoord(c.X + Around[n].dx, c.Y + Around[n].dy);
                if (!InMap(nc)) continue;
                EntityId bid = _map.BuildingAt(nc);
                if (bid.IsNone || !_buildings.Slots.IsAlive(bid)) continue;
                int b = bid.Index;
                if (_effects.BuildingBreach[b] != 0 || !BuildingTypeAt(b).Wall || _buildings.Health[b] <= 0) continue;
                if (RelationOf(p, _buildings.Player[b]) != Relation.Enemy) continue;
                _effects.BuildingBreach[b] = open;
                _effects.BuildingBreachOwner[b] = p;
            }
        }
    }
}
