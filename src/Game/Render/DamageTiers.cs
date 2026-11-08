// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Sandbox;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Render;

/// <summary>How hurt a building or vehicle looks (D-184, view only). Append only.</summary>
public enum DamageTier : byte
{
    /// <summary>Half health or more: no effect.</summary>
    None = 0,

    /// <summary>Under half health: smoke.</summary>
    Smoke = 1,

    /// <summary>Under a quarter of health: fire and heavier smoke.</summary>
    Fire = 2,
}

/// <summary>The thresholds that map health to a <see cref="DamageTier"/>. Pure.</summary>
public static class DamageTiers
{
    /// <summary>Smoke starts below this share of max health (percent, exclusive: exactly half is still clean). Same rule as the building damaged frame.</summary>
    // ASSUMED (docs/design/18_art_guidance.md D2): two tiers, 50 % and 25 %.
    public const int SmokeBelowPercent = 50;

    /// <summary>Fire starts below this share of max health (percent, exclusive).</summary>
    // ASSUMED (docs/design/18_art_guidance.md D2).
    public const int FireBelowPercent = 25;

    /// <summary>The tier of <paramref name="health"/> out of <paramref name="maxHealth"/>. An unknown maximum (0 or less) is always <see cref="DamageTier.None"/>.</summary>
    public static DamageTier For(int health, int maxHealth)
    {
        if (maxHealth <= 0) return DamageTier.None;
        if ((long)health * 100 < (long)maxHealth * FireBelowPercent) return DamageTier.Fire;
        if ((long)health * 100 < (long)maxHealth * SmokeBelowPercent) return DamageTier.Smoke;
        return DamageTier.None;
    }
}

/// <summary>One thing that got worse: a building or a unit that entered a higher tier since the last snapshot.</summary>
/// <param name="Building">True for a building (<see cref="Index"/> indexes the snapshot's buildings), false for a unit (its units).</param>
/// <param name="Index">Index into the snapshot list it came from.</param>
/// <param name="From">Tier before.</param>
/// <param name="To">Tier now (higher than <paramref name="From"/>).</param>
public readonly record struct TierChange(bool Building, int Index, DamageTier From, DamageTier To);

/// <summary>
/// Remembers each building's and vehicle's tier between snapshots so the renderer can play a one-shot burst when a higher tier is
/// entered. Only things seen before can burst: the first sighting (game start, a load, an enemy revealed by vision) records its tier silently.
/// Updated once per sim tick. Never reads or writes sim state.
/// </summary>
public sealed class DamageTierTracker
{
    private Dictionary<EntityId, DamageTier> _buildings = new();
    private Dictionary<UnitKey, DamageTier> _units = new();
    private int _tick = int.MinValue;

    /// <summary>Records <paramref name="snapshot"/> and fills <paramref name="changes"/> (cleared first) with the tier increases since the last tick.</summary>
    public void Observe(SimSnapshot snapshot, SimRules? rules, List<TierChange> changes)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(changes);
        changes.Clear();
        if (rules is null || snapshot.Tick == _tick) return;
        bool silent = snapshot.Tick < _tick || _tick == int.MinValue;   // first sight, or the game was reloaded or restarted
        _tick = snapshot.Tick;

        var buildings = new Dictionary<EntityId, DamageTier>();
        for (int i = 0; i < snapshot.Buildings.Count; i++)
        {
            BuildingView b = snapshot.Buildings[i];
            if (b.Ghost || (uint)b.TypeIndex >= (uint)rules.Buildings.Count) continue;
            DamageTier now = DamageTiers.For(b.Health, rules.Buildings[b.TypeIndex].MaxHealth);
            buildings[b.Id] = now;
            if (!silent && _buildings.TryGetValue(b.Id, out DamageTier before) && now > before) changes.Add(new TierChange(true, i, before, now));
        }

        var units = new Dictionary<UnitKey, DamageTier>();
        for (int i = 0; i < snapshot.Units.Count; i++)
        {
            UnitView u = snapshot.Units[i];
            if (u.Kind != UnitKind.Vehicle || (uint)u.TypeIndex >= (uint)rules.Vehicles.Count) continue;
            var key = UnitKey.Of(u);
            DamageTier now = DamageTiers.For(u.Health, rules.Vehicles[u.TypeIndex].MaxHealth);
            units[key] = now;
            if (!silent && _units.TryGetValue(key, out DamageTier before) && now > before) changes.Add(new TierChange(false, i, before, now));
        }

        _buildings = buildings;
        _units = units;
    }
}
