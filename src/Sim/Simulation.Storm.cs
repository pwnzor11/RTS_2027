// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Effects;
using Rts.Sim.Rules;

namespace Rts.Sim;

// M6-07 (OWNER: M6-07 package; hook placed by the lead in session 9). The Second Drift storm (10_m6_plan.md §3.2, effects v2 storms).
// Hook: StepCore -> RunStorms (after RunEffects, before combat). Firing goes through the existing superweapon path.
public sealed partial class Simulation
{
    /// <summary>Blackout ticks a storm lays each pass when its warhead sets none (the pass repeats every 10 ticks).</summary>
    internal const int StormBlackoutTicks = 20;

    /// <summary>The storm warhead of <paramref name="owner"/>'s rules: the first (lowest index) warhead with StormTicks &gt; 0.
    /// The storm table stores no warhead id (layout fixed by M6-02), so contaminate step/cap and slow come from here.</summary>
    private WarheadType? StormWarheadOf(byte owner)
    {
        IReadOnlyList<WarheadType> all = RulesOf(owner).Warheads;
        for (int w = 0; w < all.Count; w++) if (all[w].StormTicks > 0 && all[w].StormRadius > 0) return all[w];
        return null;
    }

    /// <summary>Put a storm on <paramref name="centre"/>: lowest free slot, else replace the one with the fewest ticks left (lowest slot on ties).</summary>
    private void StartStorm(byte owner, CellCoord centre, WarheadType wh)
    {
        int slot = -1;
        for (int s = 0; s < EffectsState.MaxStorms && slot < 0; s++) if (_effects.StormTicks[s] <= 0) slot = s;
        if (slot < 0)
        {
            slot = 0;
            for (int s = 1; s < EffectsState.MaxStorms; s++) if (_effects.StormTicks[s] < _effects.StormTicks[slot]) slot = s;
        }
        _effects.StormX[slot] = centre.X; _effects.StormY[slot] = centre.Y; _effects.StormRadius[slot] = wh.StormRadius;
        _effects.StormTicks[slot] = wh.StormTicks; _effects.StormOwner[slot] = owner;
    }

    /// <summary>Per tick: count storm timers down; every 10 ticks lay Blackout and contaminate (step, never above the cap) over each live storm.</summary>
    private void RunStorms()
    {
        for (int s = 0; s < EffectsState.MaxStorms; s++)
        {
            if (_effects.StormTicks[s] <= 0) continue;
            byte owner = _effects.StormOwner[s];
            if (Tick % 10 == 0)
            {
                WarheadType? wh = StormWarheadOf(owner);
                int step = wh?.StormContaminateStep ?? 0, cap = Math.Min(255, wh?.StormContaminateCap ?? 0);
                int blackout = wh != null && wh.BlackoutTicks > 0 ? wh.BlackoutTicks : StormBlackoutTicks;
                int r = _effects.StormRadius[s];
                for (int y = _effects.StormY[s] - r; y <= _effects.StormY[s] + r; y++)
                    for (int x = _effects.StormX[s] - r; x <= _effects.StormX[s] + r; x++)
                    {
                        var cell = new CellCoord(x, y);
                        if (!_ground.Contains(cell)) continue;
                        int idx = _ground.IndexOf(cell);
                        if (blackout > _ground.Blackout[idx]) _ground.Blackout[idx] = blackout;
                        int level = _ground.Radiation[idx];
                        if (step > 0 && level < cap) _ground.Radiation[idx] = (byte)Math.Min(cap, level + step);
                    }
            }
            if (--_effects.StormTicks[s] <= 0)
            {   // expired: clear the whole slot so the saved/hashed state is canonical
                _effects.StormX[s] = 0; _effects.StormY[s] = 0; _effects.StormRadius[s] = 0; _effects.StormTicks[s] = 0; _effects.StormOwner[s] = 0;
            }
        }
    }

    private bool StormCovers(int s, CellCoord c) =>
        _effects.StormTicks[s] > 0 && Math.Max(Math.Abs(c.X - _effects.StormX[s]), Math.Abs(c.Y - _effects.StormY[s])) <= _effects.StormRadius[s];

    /// <summary>Speed multiplier in permille that storms apply to a vehicle of <paramref name="player"/> at <paramref name="pos"/>
    /// (1000 = none; below 1000 inside an enemy storm). Read by the aura recompute (M6-06, Simulation.Effects.cs) every 10 ticks.
    /// Only storms of an ENEMY owner slow; Left Behind players are immune to every storm slow (design 06 sec. 6).</summary>
    internal int StormSpeedPermilleAt(byte player, LeptonCoord pos)
    {
        if (player >= _players.Count || _players.Faction[player] == Faction.LeftBehind) return 1000;
        CellCoord c = pos.ToCell();
        int best = 1000;
        for (int s = 0; s < EffectsState.MaxStorms; s++)
        {
            if (!StormCovers(s, c) || RelationOf(player, _effects.StormOwner[s]) != Relation.Enemy) continue;
            int p = StormWarheadOf(_effects.StormOwner[s])?.StormSpeedPermille ?? 0;
            if (p > 0 && p < best) best = p;
        }
        return best;
    }

    /// <summary>True if <paramref name="c"/> lies inside a live storm owned by <paramref name="player"/> or an ally: Left Behind units there get
    /// the ash stealth bonus. Not wired into RunStealth (M6-06 owns it).</summary>
    internal bool InFriendlyStorm(byte player, CellCoord c)
    {
        for (int s = 0; s < EffectsState.MaxStorms; s++)
        {
            if (!StormCovers(s, c)) continue;
            Relation r = RelationOf(player, _effects.StormOwner[s]);
            if (r == Relation.Self || r == Relation.Ally) return true;
        }
        return false;
    }
}
