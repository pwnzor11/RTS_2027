// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Veterancy;

namespace Rts.Sim;

// Generic veterancy (docs/design/14_veterancy.md, D-127). VET-1: the "veterancy" part and its accessor. VET-2 adds kill credit
// (Simulation.Combat.cs), VET-3 the rank and bonuses.
public sealed partial class Simulation
{
    /// <summary>The "veterancy" part (VET-1): kill value and last-combat tick per vehicle and infantry slot.</summary>
    public VeterancyState Veterancy => _veterancy;
}
