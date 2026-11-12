// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;

namespace Rts.Sim;

// M7-05 (docs/design/11_m7_plan.md §2.5, 07_expedition.md §6.4; D-137, D-138): submerge and sonar. The dive flag lives in the "naval" part
// (NavalState.Submerged). A submerged ship is hidden from every enemy team unless a ship of that team with a sonar_radius has it inside
// the radius (the same shape as the Seismograph and a burrowed unit, Simulation.Ground.cs); it is therefore untargetable, unlisted in
// snapshots and PlayerViews, and only a weapon that hits submerged vessels may shoot it. Own and allied ships always see it.
public sealed partial class Simulation
{
    /// <summary>True for a vehicle whose locomotion is Naval (a surface ship or a submarine).</summary>
    internal bool IsNavalShip(ActorKind kind, int i) =>
        kind == ActorKind.Vehicle && RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].Locomotion == Locomotion.Naval;

    /// <summary>True while the actor is a submarine that is dived.</summary>
    internal bool IsSubmergedShip(ActorKind kind, int i) => kind == ActorKind.Vehicle && _naval.Submerged[i];

    /// <summary>D-205 (FACTION-LE-05): how long a dived submarine stays visible and targetable after it fires: 5 seconds.</summary>
    internal const int SubRevealTicks = 5 * SimConstants.TicksPerSecond;

    /// <summary>True while a submarine is dived and has not fired in the last <see cref="SubRevealTicks"/>: the state in which it is
    /// hidden, untargetable by ordinary weapons, spared by splash and uses submerged armour.</summary>
    internal bool IsHiddenSub(ActorKind kind, int i) => kind == ActorKind.Vehicle && _naval.Submerged[i] && _naval.RevealLeft[i] == 0;

    /// <summary>True if a ship or building of the viewer's team with a sonar radius covers <paramref name="cell"/> (Chebyshev, like the Seismograph).</summary>
    internal bool IsSonarRevealed(byte viewer, CellCoord cell)
    {
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _units.Health[i] <= 0 || !IsFriendly(viewer, _units.Player[i])) continue;
            int rad = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].SonarRadius;   // assumed — confirm (M7-05): only vehicles carry sonar
            if (rad > 0 && LeptonMath.ChebyshevCells(cell, _units.Position[i].ToCell()) <= rad) return true;
        }
        for (int i = 1; i < _buildings.Capacity; i++)   // simfu-4: a building with sonar_radius (the Expedition picket)
        {
            if (!_buildings.Slots.IsIndexAlive(i) || _buildings.Health[i] <= 0 || !IsFriendly(viewer, _buildings.Player[i])) continue;
            BuildingType bt = RulesOf(_buildings.Player[i]).Buildings[_buildings.TypeIndex[i]];
            if (bt.SonarRadius <= 0) continue;
            CellCoord tl = _buildings.Cell[i];
            // Chebyshev distance from the cell to the footprint rectangle
            int dx = Math.Max(0, Math.Max(tl.X - cell.X, cell.X - (tl.X + bt.FootprintW - 1)));
            int dy = Math.Max(0, Math.Max(tl.Y - cell.Y, cell.Y - (tl.Y + bt.FootprintH - 1)));
            if (Math.Max(dx, dy) <= bt.SonarRadius) return true;
        }
        return false;
    }

    /// <summary>
    /// Weapon Targets versus the actor. Ground = anything that is not air and not submerged (land units, buildings, surface ships);
    /// Air = aircraft; Both = Ground or Air; Naval = surface ships; Submerged = dived submarines; NavalAndSubmerged = every ship.
    /// With no ships in the rules the result is exactly the old air/ground test.
    /// </summary>
    internal bool WeaponTargetsActor(WeaponType w, ActorKind kind, int i)
    {
        bool air = IsAirUnit(kind, i), sub = IsHiddenSub(kind, i);   // D-205: a sub that just fired is an ordinary surface target
        switch (w.Targets)
        {
            case WeaponTargets.Both: return !sub;
            case WeaponTargets.Air: return air;
            case WeaponTargets.Naval: return !sub && IsNavalShip(kind, i);
            case WeaponTargets.Submerged: return sub;
            case WeaponTargets.NavalAndSubmerged: return sub || IsNavalShip(kind, i);
            default: return !air && !sub;
        }
    }

    /// <summary>True if area damage skips this actor: a dived ship is only hurt when it is the shot's own target.</summary>
    internal bool SplashSparesSubmerged(ActorKind kind, int i, ActorRef primary) =>
        IsHiddenSub(kind, i) && !(primary.Kind == kind && primary.Id == IdAt(kind, i));

    /// <summary>Deploy on a ship with <c>submerges</c>: toggles dive and surface at once. Subject is an own live ship.</summary>
    private void ApplySubmerge(Command c)
    {
        int i = c.Subject.Index;
        if (!_units.Slots.IsAlive(c.Subject) || _units.Player[i] != c.Player || _units.Health[i] <= 0) return;
        if (!RulesOf(c.Player).Vehicles[_units.TypeIndex[i]].Submerges || _tunnels.VehicleLayer[i] != ActorLayer.Surface) return;
        _naval.Submerged[i] = !_naval.Submerged[i];   // assumed — confirm (M7-05): instant, no dive animation or delay; no automatic dive
    }
}
