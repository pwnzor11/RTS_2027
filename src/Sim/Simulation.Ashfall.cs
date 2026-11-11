// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
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
}
