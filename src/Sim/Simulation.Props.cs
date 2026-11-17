// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Map;
using Rts.Sim.Rules;

namespace Rts.Sim;

// M12-16 (D-264 (3), design 16 D1 option C): destructible blocking props. Health lives in the "props" part (PropState); the rules are
// game.toml [props] (PropRules).
//   Splash damage: Detonate (splash >= 1: shells, death weapons), the superweapon blast and a salvo impact call DamageProps. Instant
//   small-arms hits (splash 0) do nothing to props. Tree, Snag and Wreck have health; Block and Pylon never break.
//   Crushing: a heavy tracked crusher (MoveClass.TrackedCrusher) entering a Tree or Snag cell destroys it (CrushPropAt, from MoveMission.Reserve).
//   A destroyed prop clears the overlay, leaves Rough ground and emits FxKind.PropDestroyed.
// Props are neutral map objects, not actors: damage is applied at once (a sum of subtractions, so the order of the hits of one tick does not
// matter) and the AI never targets them.
public sealed partial class Simulation
{
    /// <summary>The "props" part: the health of every destructible prop (Tree, Snag, Wreck) on the map (M12-16).</summary>
    public PropState Props => _props;

    private PropRules PropRulesOf => Setup.Rules.Props;   // global rule block, not upgradable: the only read of Setup.Rules.Props

    /// <summary>
    /// A splash of <paramref name="radius"/> cells (Chebyshev) around <paramref name="centre"/> hurts every destructible prop in range:
    /// <c>damage x versus[SplashArmor] / 100</c> on a cell within <paramref name="fullRadius"/> of the centre, half of it (minimum 1) beyond,
    /// exactly as <c>Hurt</c> falls off for actors. A warhead whose versus is 0 for the props' armour class does nothing. Cells are visited in
    /// index order.
    /// </summary>
    internal void DamageProps(CellCoord centre, int radius, int damage, WarheadType wh, int fullRadius)
    {
        if (radius < 0 || damage <= 0) return;
        PropRules rules = PropRulesOf;
        int full = ComputeDamage(damage, wh, rules.SplashArmor);
        if (full <= 0) return;
        int half = Math.Max(1, full / 2);
        int w = Setup.Map.Width;
        for (int y = Math.Max(0, centre.Y - radius); y <= Math.Min(Setup.Map.Height - 1, centre.Y + radius); y++)
            for (int x = Math.Max(0, centre.X - radius); x <= Math.Min(w - 1, centre.X + radius); x++)
            {
                var cell = new CellCoord(x, y);
                OverlayKind o = _map.OverlayAt(cell);
                int i = y * w + x;
                if (!OverlayKinds.IsDestructible(o)) { _props.Health[i] = 0; continue; }   // not (or no longer) a destructible prop
                if (_props.Health[i] == 0) _props.Health[i] = (ushort)Math.Clamp(rules.HealthOf(o), 1, ushort.MaxValue);   // a prop put there after setup starts whole
                int d = Math.Max(Math.Abs(x - centre.X), Math.Abs(y - centre.Y));
                int hit = d > fullRadius ? half : full;
                if (hit >= _props.Health[i]) DestroyProp(cell);
                else _props.Health[i] = (ushort)(_props.Health[i] - hit);
            }
    }

    /// <summary>A heavy crusher entered <paramref name="cell"/>: a Tree or Snag there is destroyed (anything else is left alone).</summary>
    internal void CrushPropAt(CellCoord cell)
    {
        if (InMap(cell) && OverlayKinds.IsCrushable(_map.OverlayAt(cell))) DestroyProp(cell);
    }

    /// <summary>
    /// Removes the prop on <paramref name="cell"/>: health 0, overlay cleared, and Clear, Road or Ash land becomes Rough (ASSUMED S31: Rock,
    /// Cliff, water and coast keep their land, so a prop never opens impassable ground). Bumps the map version through the overlay and land
    /// setters, which refreshes every cached path and region label. Emits <see cref="FxKind.PropDestroyed"/>.
    /// </summary>
    private void DestroyProp(CellCoord cell)
    {
        _props.Health[cell.Y * Setup.Map.Width + cell.X] = 0;
        _map.SetOverlay(cell, OverlayKind.None, 0);
        if (_map.LandAt(cell) is LandType.Clear or LandType.Road or LandType.Ash) _map.SetLand(cell, LandType.Rough);
        _fx.Add(new FxEvent(FxKind.PropDestroyed, CellCentre(cell), 1));
    }
}
