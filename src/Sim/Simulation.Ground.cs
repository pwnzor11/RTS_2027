// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Radiation, Blackout, Seismograph, Searchlight, stealth and Static Broadcast (design 05 §3, D-077). OWNER: M3-05.
// State: Ground.Radiation / Ground.Blackout, Effects.*Still. Constants: Setup.Rules.Radiation / Setup.Rules.Stealth.
// Warhead contamination/Blackout is applied from Detonate (Simulation.Combat.cs) — M3-05 adds that call there.
public sealed partial class Simulation
{
    /// <summary>Per tick (in RunGround): radiation decay every DecayTicks, damage every DamageIntervalTicks.</summary>
    private void RunRadiation()
    {
        // M3-05 fills this
    }

    /// <summary>Per tick (in RunGround): count Blackout timers down.</summary>
    private void RunBlackout()
    {
        // M3-05 fills this
    }

    /// <summary>Per tick (in RunEffects): advance/reset stealth timers (Effects.VehicleStill / InfantryStill).</summary>
    private void RunStealth()
    {
        // M3-05 fills this
    }

    /// <summary>True if a Seismograph of the viewer's team reveals burrowed units and telegraphs on <paramref name="cell"/>.</summary>
    internal bool IsSeismicRevealed(byte viewer, CellCoord cell)
    {
        // M3-05 fills this
        return false;
    }

    /// <summary>True if the stealth actor is currently invisible to enemies (Stealth type and timer reached).</summary>
    internal bool IsStealthedNow(ActorKind kind, int i)
    {
        // M3-05 fills this
        return false;
    }

    /// <summary>True if the viewer's team reveals stealthed actors on <paramref name="cell"/> (within 1 cell, Searchlight sight).</summary>
    internal bool IsStealthRevealed(byte viewer, CellCoord cell)
    {
        // M3-05 fills this
        return false;
    }

    /// <summary>True if <paramref name="cell"/> is inside an enemy jam radius for <paramref name="viewer"/> (minimap only).</summary>
    internal bool IsJammedFor(byte viewer, CellCoord cell)
    {
        // M3-05 fills this
        return false;
    }
}
