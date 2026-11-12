// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Numerics;
using Rts.Sim.Rules;
using Rts.Sim.World;

namespace Rts.Sim;

// Radiation, Blackout, Seismograph, Searchlight, stealth and Static Broadcast (design 05 §3, D-077). OWNER: M3-05.
// State: Ground.Radiation / Ground.Blackout, Effects.*Still. Constants: Setup.Rules.Radiation / Setup.Rules.Stealth.
// Warhead contamination/Blackout is applied from Detonate (Simulation.Combat.cs) via ApplyWarheadGround.
public sealed partial class Simulation
{
    // ---- warhead ground effects -----------------------------------------------------------------

    /// <summary>M4: lay Quick-Set Foam (<see cref="WarheadType.FoamTicks"/>) on every on-map cell within <paramref name="radius"/>
    /// cells of <paramref name="centre"/>. Called from Detonate after <see cref="ApplyWarheadGround"/>. M4-05 fills.</summary>
    internal void ApplyWarheadFoam(WarheadType wh, CellCoord centre, int radius)
    {
        if (wh.FoamTicks <= 0) return;
        for (int y = centre.Y - radius; y <= centre.Y + radius; y++)
            for (int x = centre.X - radius; x <= centre.X + radius; x++)
            {
                var c = new CellCoord(x, y);
                if (!_ground.Contains(c)) continue;
                int idx = _ground.IndexOf(c);
                if (wh.FoamTicks > _ground.Foam[idx]) _ground.Foam[idx] = wh.FoamTicks;   // re-foaming refreshes, never shortens
            }
    }

    /// <summary>True if Quick-Set Foam covers the cell (M4-05): burrowers cannot surface and Bores cannot break through there,
    /// and ground units move at <c>[foam] speed_permille</c>.</summary>
    internal bool IsFoamed(CellCoord c) => _ground.FoamAt(c) > 0;

    /// <summary>Apply a warhead's Contaminate / Decontaminate / Blackout to every on-map cell within <paramref name="radius"/>
    /// cells (Chebyshev) of <paramref name="centre"/>. Contaminate runs first, then Decontaminate.</summary>
    internal void ApplyWarheadGround(WarheadType wh, CellCoord centre, int radius)
    {
        if (wh.Contaminate <= 0 && wh.Decontaminate <= 0 && wh.BlackoutTicks <= 0) return;
        for (int y = centre.Y - radius; y <= centre.Y + radius; y++)
            for (int x = centre.X - radius; x <= centre.X + radius; x++)
            {
                var c = new CellCoord(x, y);
                if (!_ground.Contains(c)) continue;
                int idx = _ground.IndexOf(c);
                int level = _ground.Radiation[idx];
                if (wh.Contaminate > 0) level = Math.Min(255, level + wh.Contaminate);
                if (wh.Decontaminate > 0) level = Math.Max(0, level - wh.Decontaminate);
                _ground.Radiation[idx] = (byte)level;
                if (wh.BlackoutTicks > _ground.Blackout[idx]) _ground.Blackout[idx] = wh.BlackoutTicks;
            }
    }

    /// <summary>True if the mobile or building actor counts as irradiated for warhead bonuses: an Irradiated type, or standing on
    /// a cell at or above the radiation damage threshold (design 05 §3).</summary>
    internal bool IsIrradiatedTarget(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        bool typed = kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].Irradiated
            : kind == ActorKind.Infantry && r.Infantry[_infantry.TypeIndex[i]].Irradiated;
        if (typed) return true;
        CellCoord cell = kind == ActorKind.Building ? _buildings.Cell[i] : PositionAt(kind, i).ToCell();
        return _ground.RadiationAt(cell) >= Setup.Rules.Radiation.DamageThreshold;
    }

    /// <summary>Per tick (in RunGround): radiation decay every DecayTicks, damage every DamageIntervalTicks.</summary>
    private void RunRadiation()
    {
        RadiationRules rr = Setup.Rules.Radiation;
        if (rr.DecayTicks > 0 && Tick % rr.DecayTicks == 0)
        {
            byte[] rad = _ground.Radiation;
            for (int i = 0; i < rad.Length; i++) if (rad[i] > 0) rad[i]--;
        }
        if (rr.DamageIntervalTicks <= 0 || Tick % rr.DamageIntervalTicks != 0) return;
        for (int k = 0; k < 2; k++)   // vehicles, infantry; structures are not hurt (ASSUMED below)
        {
            var kind = (ActorKind)k;
            MobileStore store = kind == ActorKind.Vehicle ? _units : _infantry;
            for (int i = 1; i < store.Capacity; i++)
            {
                if (!store.Slots.IsIndexAlive(i) || store.Health[i] <= 0) continue;
                // ASSUMED (M3-05): underground actors (tunnel, burrowed, passenger) take no radiation damage; buildings take none.
                if (IsUnderground(kind, i)) continue;
                int level = _ground.RadiationAt(store.Position[i].ToCell());
                if (level < rr.DamageThreshold) continue;
                SimRules r = RulesOf(store.Player[i]);
                int resist = kind == ActorKind.Vehicle ? r.Vehicles[store.TypeIndex[i]].RadResistPermille
                                                       : r.Infantry[store.TypeIndex[i]].RadResistPermille;
                if (resist >= 1000) continue;
                int dmg = level * rr.DamagePermille / 1000 * (1000 - Math.Max(0, resist)) / 1000;
                if (dmg < 1) dmg = 1;   // ASSUMED (M3-05): a non-immune actor on a hot cell always takes at least 1
                store.Health[i] -= dmg;
            }
        }
    }

    /// <summary>Per tick (in RunGround): count Blackout timers down.</summary>
    private void RunBlackout()
    {
        int[] b = _ground.Blackout;
        for (int i = 0; i < b.Length; i++) if (b[i] > 0) b[i]--;
        // M4-05: Quick-Set Foam counts down one per tick, in the same pass (RunGround lives in Simulation.M3.cs, not owned here).
        int[] f = _ground.Foam;
        for (int i = 0; i < f.Length; i++) if (f[i] > 0) f[i]--;
    }

    /// <summary>True if a viewer-team actor can see a cell inside a Blackout cloud: any team actor within 1 cell, or a team
    /// building with SeesThroughBlackout whose sight covers it.</summary>
    internal bool CanSeeThroughBlackout(byte viewer, CellCoord cell)
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                if (!slots.IsIndexAlive(i) || !IsFriendly(viewer, PlayerAt(kind, i))) continue;
                int d = LeptonMath.ChebyshevCells(cell, VisionCellOf(kind, i));
                if (d <= 1) return true;
                // ASSUMED (M3-05): Searchlight sight is a square (Chebyshev) of the building's Sight radius.
                if (kind == ActorKind.Building && BuildingTypeAt(i).SeesThroughBlackout && !IsUnpowered(kind, i) && d <= SightOf(kind, i)) return true;
            }
        }
        return false;
    }

    // ---- stealth ------------------------------------------------------------------------------------

    /// <summary>Per tick (in RunEffects): advance/reset stealth timers (Effects.VehicleStill / InfantryStill).</summary>
    private void RunStealth()
    {
        int delay = Setup.Rules.Stealth.DelayTicks;   // the still counter caps here; readers compare with StealthDelayFor
        for (int i = 1; i < _naval.RevealLeft.Length; i++) if (_naval.RevealLeft[i] > 0) _naval.RevealLeft[i]--;   // D-205: a firing sub's reveal runs out
        for (int k = 0; k < 2; k++)
        {
            var kind = (ActorKind)k;
            MobileStore store = kind == ActorKind.Vehicle ? _units : _infantry;
            int[] still = kind == ActorKind.Vehicle ? _effects.VehicleStill : _effects.InfantryStill;
            LeptonCoord[] start = kind == ActorKind.Vehicle ? _tickPosVeh : _tickPosInf;
            for (int i = 1; i < store.Capacity; i++)
            {
                if (!store.Slots.IsIndexAlive(i) || !IsStealthType(kind, i)) { if (still[i] != 0 && store.Slots.IsIndexAlive(i)) still[i] = 0; continue; }
                bool moved = _useTickPos && i < start.Length && store.Position[i] != start[i];
                bool fired = false;
                int weapon = WeaponOf(kind, i);
                if (weapon >= 0)
                {
                    int rof = RulesOf(store.Player[i]).Weapons[weapon].RateOfFire;
                    fired = _combat.At(kind, i).ReloadTicks >= Math.Max(1, rof);   // Fire set the reload this tick
                }
                if (moved || fired) still[i] = 0;
                else if (still[i] < delay) still[i]++;
            }
        }
        LayBlackoutTrails();
    }

    /// <summary>M6-06: stealth delay for one actor. Infantry with AshStealthDelayTicks &gt; 0 standing on an Ashfall cell whose density is
    /// above AshStealthThreshold use that delay when it is shorter than the global one (read-only map lookup). The still counter is
    /// capped at the global delay, so a reading against the shorter delay is consistent.</summary>
    private int StealthDelayFor(ActorKind kind, int i)
    {
        int delay = Setup.Rules.Stealth.DelayTicks;
        if (kind != ActorKind.Infantry) return delay;
        InfantryType t = RulesOf(_infantry.Player[i]).Infantry[_infantry.TypeIndex[i]];
        if (t.AshStealthDelayTicks <= 0 || t.AshStealthDelayTicks >= delay) return delay;
        CellCoord c = _infantry.Position[i].ToCell();
        bool ashy = _map.OverlayAt(c) == OverlayKind.Ashfall && _map.DensityAt(c) > t.AshStealthThreshold;
        if (!ashy && !InFriendlyStorm(_infantry.Player[i], c)) return delay;   // M6-07: a friendly storm counts as ash (design 06 §6)
        return t.AshStealthDelayTicks;
    }

    /// <summary>M6-06: a moving surface vehicle with DropsBlackoutTicks lays Blackout on its current cell (max with what is there) on
    /// the tick it enters a new cell and every 10th tick otherwise. Runs in slot order inside RunStealth, where the start-of-tick
    /// positions are still valid; the cell timers are decremented later in RunBlackout.</summary>
    private void LayBlackoutTrails()
    {
        if (!_useTickPos) return;
        for (int i = 1; i < _units.Capacity; i++)
        {
            if (!_units.Slots.IsIndexAlive(i) || _units.Health[i] <= 0 || i >= _tickPosVeh.Length || _units.Position[i] == _tickPosVeh[i]) continue;
            int ticks = RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].DropsBlackoutTicks;
            if (ticks <= 0 || _tunnels.LayerOf(ActorKind.Vehicle, i) != ActorLayer.Surface) continue;
            CellCoord cell = _units.Position[i].ToCell();
            if (Tick % AuraIntervalTicks != 0 && cell == _tickPosVeh[i].ToCell()) continue;
            if (!_ground.Contains(cell)) continue;
            int idx = _ground.IndexOf(cell);
            if (ticks > _ground.Blackout[idx]) _ground.Blackout[idx] = ticks;
        }
    }

    private bool IsStealthType(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].Stealth
            : kind == ActorKind.Infantry && r.Infantry[_infantry.TypeIndex[i]].Stealth;
    }

    /// <summary>True if the stealth actor is currently invisible to enemies (Stealth type and timer reached).</summary>
    internal bool IsStealthedNow(ActorKind kind, int i)
    {
        if (kind == ActorKind.Building || !IsStealthType(kind, i)) return false;
        int still = kind == ActorKind.Vehicle ? _effects.VehicleStill[i] : _effects.InfantryStill[i];
        return still >= StealthDelayFor(kind, i);
    }

    /// <summary>Radiation level at which an attack-ground Ash Sower considers a cell sown (ASSUMED, S19): stops it killing itself on an
    /// endless order.</summary>
    private const int SownRadiation = 128;

    /// <summary>S19 (FAD-05): true when a ground-effect weapon (damage 0: decon, foam, sow, blackout) has done its job on the cell, so an
    /// attack-ground order with it can end: decon = cell clean, foam = foamed, sow = radiation at <see cref="SownRadiation"/>, blackout = under cloud.
    /// A weapon that deals damage never ends by itself.</summary>
    internal bool GroundEffectDone(byte owner, WeaponType w, CellCoord cell)
    {
        if (w.Damage > 0 || !_ground.Contains(cell)) return false;
        WarheadType wh = RulesOf(owner).Warheads[w.Warhead];
        if (wh.Decontaminate > 0) return _ground.RadiationAt(cell) == 0;
        if (wh.FoamTicks > 0) return IsFoamed(cell);
        if (wh.Contaminate > 0) return _ground.RadiationAt(cell) >= SownRadiation;
        if (wh.BlackoutTicks > 0) return _ground.BlackoutAt(cell) > 0;
        return false;
    }

    /// <summary>S19 FAD-19: a building whose type needs power and whose owner is at a low-power band does nothing (Searchlight sight,
    /// Broadcast Mast jam), like an unpowered turret holding fire. Mobile actors are never unpowered.</summary>
    private bool IsUnpowered(ActorKind kind, int i) =>
        kind == ActorKind.Building && BuildingTypeAt(i).NeedsPower && PowerBand(_buildings.Player[i]) >= 1;

    /// <summary>True if the viewer's team reveals stealthed actors on <paramref name="cell"/> (within 1 cell, Searchlight sight).</summary>
    internal bool IsStealthRevealed(byte viewer, CellCoord cell)
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                if (!slots.IsIndexAlive(i) || !IsFriendly(viewer, PlayerAt(kind, i))) continue;
                int d = LeptonMath.ChebyshevCells(cell, VisionCellOf(kind, i));
                if (d <= 1) return true;
                if (kind == ActorKind.Building && BuildingTypeAt(i).SeesThroughBlackout && !IsUnpowered(kind, i) && d <= SightOf(kind, i)) return true;
            }
        }
        return false;
    }

    // ---- seismic and jamming ---------------------------------------------------------------------------

    private int SeismicRadiusOf(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].SeismicRadius
            : kind == ActorKind.Infantry ? r.Infantry[_infantry.TypeIndex[i]].SeismicRadius
            : r.Buildings[_buildings.TypeIndex[i]].SeismicRadius;
    }

    private int JamRadiusOf(ActorKind kind, int i)
    {
        SimRules r = RulesOf(PlayerAt(kind, i));
        return kind == ActorKind.Vehicle ? r.Vehicles[_units.TypeIndex[i]].JamRadius
            : kind == ActorKind.Infantry ? r.Infantry[_infantry.TypeIndex[i]].JamRadius
            : r.Buildings[_buildings.TypeIndex[i]].JamRadius;
    }

    // M3-INT: IsSeismicRevealed / IsJammedFor used to scan every actor per call. They now walk only the "emitters" (actors whose
    // type has a seismic or jam radius). The list is lazy scratch (not hashed, not saved): it only ever grows between rebuilds,
    // and each query still re-reads liveness, owner, radius and position, so results are identical to a full scan.
    // _emittersDirty is raised wherever an actor is added, changes type or changes owner (and on load).
    private readonly List<(ActorKind Kind, int Index)> _emitters = new();
    private bool _emittersDirty = true;

    /// <summary>Test hook: when false, <see cref="IsSeismicRevealed"/> and <see cref="IsJammedFor"/> scan every actor (the old behaviour).</summary>
    internal bool UseEmitterCache { get; set; } = true;

    private void RefreshEmitters()
    {
        if (!_emittersDirty) return;
        _emitters.Clear();
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
                if (slots.IsIndexAlive(i) && (SeismicRadiusOf(kind, i) > 0 || JamRadiusOf(kind, i) > 0)) _emitters.Add((kind, i));
        }
        _emittersDirty = false;
    }

    /// <summary>True if a Seismograph of the viewer's team reveals burrowed units and telegraphs on <paramref name="cell"/>.</summary>
    internal bool IsSeismicRevealed(byte viewer, CellCoord cell)
    {
        if (!UseEmitterCache) return IsSeismicRevealedScan(viewer, cell);
        RefreshEmitters();
        foreach ((ActorKind kind, int i) in _emitters)
        {
            if (!SlotsOf(kind).IsIndexAlive(i) || !IsFriendly(viewer, PlayerAt(kind, i))) continue;
            int rad = SeismicRadiusOf(kind, i);
            if (rad > 0 && LeptonMath.ChebyshevCells(cell, VisionCellOf(kind, i)) <= rad) return true;
        }
        return false;
    }

    /// <summary>True if <paramref name="cell"/> is inside an enemy jam radius for <paramref name="viewer"/> (minimap only).</summary>
    internal bool IsJammedFor(byte viewer, CellCoord cell)
    {
        if (!UseEmitterCache) return IsJammedForScan(viewer, cell);
        RefreshEmitters();
        foreach ((ActorKind kind, int i) in _emitters)
        {
            if (!SlotsOf(kind).IsIndexAlive(i) || RelationOf(viewer, PlayerAt(kind, i)) != Relation.Enemy || IsUnpowered(kind, i)) continue;
            int rad = JamRadiusOf(kind, i);
            if (rad > 0 && LeptonMath.ChebyshevCells(cell, VisionCellOf(kind, i)) <= rad) return true;
        }
        return false;
    }

    private bool IsSeismicRevealedScan(byte viewer, CellCoord cell)
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                if (!slots.IsIndexAlive(i) || !IsFriendly(viewer, PlayerAt(kind, i))) continue;
                int rad = SeismicRadiusOf(kind, i);
                if (rad > 0 && LeptonMath.ChebyshevCells(cell, VisionCellOf(kind, i)) <= rad) return true;
            }
        }
        return false;
    }

    private bool IsJammedForScan(byte viewer, CellCoord cell)
    {
        for (int k = 0; k < LiveKinds; k++)
        {
            var kind = (ActorKind)k;
            SlotArray slots = SlotsOf(kind);
            for (int i = 1; i < slots.Capacity; i++)
            {
                if (!slots.IsIndexAlive(i) || RelationOf(viewer, PlayerAt(kind, i)) != Relation.Enemy || IsUnpowered(kind, i)) continue;
                int rad = JamRadiusOf(kind, i);
                if (rad > 0 && LeptonMath.ChebyshevCells(cell, VisionCellOf(kind, i)) <= rad) return true;
            }
        }
        return false;
    }

    // ---- targeting helpers (used by Simulation.Combat.cs) ---------------------------------------------

    /// <summary>True if the actor is an air unit (vehicle with Locomotion.Air). Infantry and buildings are ground.</summary>
    internal bool IsAirUnit(ActorKind kind, int i) =>
        kind == ActorKind.Vehicle && RulesOf(_units.Player[i]).Vehicles[_units.TypeIndex[i]].Locomotion == Locomotion.Air;

    /// <summary>True if the weapon's Targets allows the actor (air vs ground).</summary>
    internal bool WeaponCanTarget(WeaponType w, ActorKind kind, int i) =>
        WeaponTargetsActor(w, kind, i);   // M7-05: Simulation.Submerge.cs

    /// <summary>True if <paramref name="player"/> may not target the actor: underground, or hidden from the player's team.</summary>
    internal bool IsUntargetableBy(byte player, ActorKind kind, int i) =>
        kind != ActorKind.Building && (IsUnderground(kind, i) || _tunnels.LayerOf(kind, i) == ActorLayer.Garrisoned   // M4: like Passenger
            || IsHiddenFrom(player, kind, i));
}
