// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Production;
using Rts.Sim.Rules;

namespace Rts.Sim;

// Upgrade research (design 05 §4). OWNER: M3-06. Queue: ProductionState lane ProductionCategory.Upgrade (index 5, queue entry =
// upgrade index). Finishing sets Players.Upgrades[player] |= 1UL << index.
// ASSUMED (M3-06): BuildingType.Researches is a bool, so "a building that researches the upgrade" means any alive own building with
// Researches set; the upgrade's own faction gates who may research it, not the building's.
// ASSUMED (M3-06): research speed does not scale with the number of research buildings (one lane, factories = 1); low power slows it
// like any other production.
// ASSUMED (M3-06): an upgrade already in the queue cannot be queued again.
// M6-06: UpgradeType.RepairsVehiclesAtRange needs no write at completion: the mask bit is read by the aura recompute (Simulation.Effects.cs,
// HasUpgradeFlag) every 10 ticks, so it applies to units already built and to later ones alike.
public sealed partial class Simulation
{
    /// <summary>Build with Arg = ProductionCategory.Upgrade (Arg2 = upgrade index).</summary>
    private void ApplyBuildUpgrade(Command c)
    {
        if (!CanResearch(c.Player, c.Arg2)) return;
        int lane = ProductionState.Lane(c.Player, ProductionCategory.Upgrade);
        int n = _production.QueueCount[lane];
        _production.QueueType[lane * _production.MaxQueue + n] = c.Arg2;
        _production.QueueCount[lane] = n + 1;
    }

    /// <summary>Cancel with Arg = ProductionCategory.Upgrade (Arg2 = queue position). Cancelling the front item refunds what it has
    /// already drained, like any other lane.</summary>
    private void ApplyCancelUpgrade(Command c)
    {
        if (c.Player >= _players.Count || _players.IsNeutral(c.Player)) return;
        int lane = ProductionState.Lane(c.Player, ProductionCategory.Upgrade);
        if (c.Arg2 < 0 || c.Arg2 >= _production.QueueCount[lane]) return;
        if (c.Arg2 == 0) Refund(c.Player, _production.Spent[lane]);
        PopQueue(lane, c.Arg2);
    }

    /// <summary>True if the player owns an alive building that researches upgrades.</summary>
    private bool OwnsResearchBuilding(byte player)
    {
        for (int i = 1; i < _buildings.Capacity; i++)
            if (_buildings.Slots.IsIndexAlive(i) && _buildings.Player[i] == player && _buildings.Health[i] > 0 && BuildingTypeAt(i).Researches)
                return true;
        return false;
    }

    /// <summary>Upgrade prerequisites: an entry naming an upgrade needs that upgrade researched; any other entry is a type id or
    /// provides name, satisfied by an alive own building (same rule as units).</summary>
    private bool UpgradePrerequisitesMet(byte player, IReadOnlyList<string> prerequisites)
    {
        for (int k = 0; k < prerequisites.Count; k++)
        {
            int u = Setup.Rules.UpgradeIndex(prerequisites[k]);
            if (u >= 0 ? !HasUpgrade(player, u) : !OwnsProvider(player, prerequisites[k])) return false;
        }
        return true;
    }

    private bool UpgradeQueued(byte player, int upgradeIndex)
    {
        int lane = ProductionState.Lane(player, ProductionCategory.Upgrade);
        for (int k = 0; k < _production.QueueCount[lane]; k++)
            if (_production.QueueType[lane * _production.MaxQueue + k] == upgradeIndex) return true;
        return false;
    }

    /// <summary>Per tick (after RunProductionTick): progress the Upgrade lane of every player. Mirrors AdvanceLane: the cost drains in
    /// proportion to progress, the lane stalls when the player cannot pay, and it pauses (no drain, no progress) while the player has no
    /// research building.</summary>
    private void RunUpgradeResearch()
    {
        ProductionState s = _production;
        IReadOnlyList<UpgradeType> upgrades = Setup.Rules.Upgrades;
        for (int p = 0; p < _players.Count; p++)
        {
            var player = (byte)p;
            if (_players.IsNeutral(player) || _players.Defeated[p]) continue;
            int lane = ProductionState.Lane(p, ProductionCategory.Upgrade);
            if (s.QueueCount[lane] == 0) continue;
            int type = s.QueueType[lane * s.MaxQueue];
            if (type < 0 || type >= upgrades.Count || HasUpgrade(player, type))
            {
                if (s.Spent[lane] > 0) Refund(player, s.Spent[lane]);
                PopQueue(lane, 0);
                continue;
            }
            if (s.Held[lane] || !OwnsResearchBuilding(player)) continue;   // S15: a held research pays nothing
            UpgradeType u = upgrades[type];
            int total = ProductionTotalTicks(player, u.Cost, u.BuildTime, 1);
            if (s.Total[lane] != total)
            {
                if (s.Total[lane] > 0) s.Ticks[lane] = (int)((long)s.Ticks[lane] * total / s.Total[lane]);
                s.Total[lane] = total;
            }
            if (s.Ticks[lane] < total)
            {
                int want = (int)((long)u.Cost * (s.Ticks[lane] + 1) / total);
                int due = Math.Max(0, want - s.Spent[lane]);
                int paid = Spend(player, due);
                s.Spent[lane] += paid;
                if (paid < due) { s.Stalled[lane] = true; continue; }
                s.Stalled[lane] = false;
                s.Ticks[lane]++;
            }
            if (s.Ticks[lane] < total) continue;
            _players.Upgrades[p] |= 1UL << type;
            if (u.Effect == "grouting") _ground.HardeningDirty = true;   // ASSUMED (M3-06): harmless nudge for M3-04's enclosure recompute
            PopQueue(lane, 0);
        }
    }

    /// <summary>
    /// True if a Build(Upgrade, <paramref name="upgradeIndex"/>) by <paramref name="player"/> would be accepted now: the upgrade exists,
    /// its faction is the player's, it is not yet researched or queued, its prerequisites are met, the player owns a researching
    /// building and the lane has room. Feeds <see cref="PlayerHud.BuildableUpgrades"/>.
    /// </summary>
    public bool CanResearch(byte player, int upgradeIndex)
    {
        if (player >= _players.Count || _players.IsNeutral(player) || _players.Defeated[player]) return false;
        IReadOnlyList<UpgradeType> upgrades = Setup.Rules.Upgrades;
        if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count || upgradeIndex >= RuleCounts.MaxUpgrades) return false;
        UpgradeType u = upgrades[upgradeIndex];
        if (u.Faction != _players.Faction[player] || HasUpgrade(player, upgradeIndex) || UpgradeQueued(player, upgradeIndex)) return false;
        int lane = ProductionState.Lane(player, ProductionCategory.Upgrade);
        if (_production.QueueCount[lane] >= _production.MaxQueue || _production.QueueCount[lane] >= Setup.Rules.Economy.MaxQueue) return false;
        return OwnsResearchBuilding(player) && UpgradePrerequisitesMet(player, u.Prerequisites);
    }
}
