// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Missions;
using Rts.Sim.Rules;

namespace Rts.Sim;

// S19 D-205 (M13-12, D-028): "ashfall is hazardous to unprotected infantry". D-028 calls pitchblende the rich tier of the SAME resource,
// so every field cell (ashfall or pitchblende overlay) is hot. No state: the hit lands on ticks where Tick % interval == 0.
public sealed partial class Simulation
{
    /// <summary>Per tick (in RunGround): every interval, each soldier on a field cell loses <c>AshfallDamage</c> scaled by
    /// (1000 - rad resistance) / 1000. Vehicles, buildings, underground and carried soldiers are unaffected; a soldier with full
    /// resistance takes nothing; anyone else takes at least 1.</summary>
    private void RunAshfall()
    {
        RadiationRules rr = Setup.Rules.Radiation;
        if (rr.AshfallDamage <= 0 || rr.AshfallIntervalTicks <= 0 || Tick % rr.AshfallIntervalTicks != 0) return;
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _infantry.Health[i] <= 0) continue;
            if (IsUnderground(ActorKind.Infantry, i) || ActorLayers.IsCarried(_tunnels.LayerOf(ActorKind.Infantry, i))) continue;
            OverlayKind field = _map.OverlayAt(_infantry.Position[i].ToCell());
            if (field != OverlayKind.Ashfall && field != OverlayKind.Pitchblende) continue;
            int resist = RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]].RadResistPermille;
            if (resist >= 1000) continue;
            int dmg = rr.AshfallDamage * (1000 - Math.Max(0, resist)) / 1000;
            _infantry.Health[i] -= dmg < 1 ? 1 : dmg;
        }
    }

    /// <summary>Chebyshev radius an idle soldier looks for a clean cell in (ASSUMED, S19 M13-12; owner to confirm).</summary>
    internal const int AshStepOffRadius = 3;

    /// <summary>
    /// S19 M13-12 (owner option a, ASSUMED - confirm): once per ashfall interval, half-way between two hits, an idle soldier standing on a
    /// field cell walks to the nearest clean passable cell within <see cref="AshStepOffRadius"/> (rings outward, each ring row by row).
    /// Idle = Guard or no mission, nothing queued, no target, not moving. A soldier that was ordered onto this cell (its last path goal or
    /// attack-move point) stays, so player orders onto ash are respected; carried, garrisoned, underground and fully resistant soldiers
    /// and soldiers that hide in ash (AshStealthDelayTicks, M6-06) are left alone. Runs right after input (before missions) with the ordinary Move order, so RunMovement resolves it this tick.
    /// </summary>
    private void RunAshStepOff()
    {
        RadiationRules rr = Setup.Rules.Radiation;
        if (rr.AshfallDamage <= 0 || rr.AshfallIntervalTicks <= 0 || Tick % rr.AshfallIntervalTicks != rr.AshfallIntervalTicks / 2) return;
        for (int i = 1; i < _infantry.Capacity; i++)
        {
            if (!_infantry.Slots.IsIndexAlive(i) || _infantry.Health[i] <= 0) continue;
            CellCoord cell = _infantry.Position[i].ToCell();
            if (!OverlayKinds.IsResource(_map.OverlayAt(cell))) continue;
            if (_infantry.Mission[i] is not (MissionKind.Guard or MissionKind.None) || _infantry.NextMission[i] != MissionKind.None) continue;
            if (_infantry.PathActive[i] || _infantry.PathGoal[i] == cell) continue;
            ref Combat.CombatEntry e = ref _combat.At(ActorKind.Infantry, i);
            if (!e.AttackTarget.Id.IsNone || e.GuardPoint == cell) continue;
            if (IsUnderground(ActorKind.Infantry, i) || ActorLayers.IsCarried(_tunnels.LayerOf(ActorKind.Infantry, i))) continue;
            InfantryType it = RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]];
            if (it.RadResistPermille >= 1000 || it.AshStealthDelayTicks > 0) continue;   // immune, or hides in ash by design (M6-06): stays
            if (NearestCleanCell(cell) is not CellCoord to) continue;
            ApplyMovementCommand(new Command(Tick, _infantry.Player[i], 0, CommandKind.Move, _infantry.Slots.IdOf(i), EntityId.None, to, (int)ActorKind.Infantry));
        }
    }

    private CellCoord? NearestCleanCell(CellCoord from)
    {
        for (int r = 1; r <= AshStepOffRadius; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                    var c = new CellCoord(from.X + dx, from.Y + dy);
                    if (!InMap(c) || OverlayKinds.IsResource(_map.OverlayAt(c)) || !_map.BuildingAt(c).IsNone) continue;
                    if (_map.SpeedPercent(c, Locomotion.Foot, Setup.Rules.Terrain) > 0) return c;
                }
        return null;
    }
}
