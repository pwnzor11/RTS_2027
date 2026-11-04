// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim;

// Superweapon (design 05 §4: the Sunrise Device). OWNER: M3-13 (wave 2). State: Effects.BuildingCharge.
public sealed partial class Simulation
{
    /// <summary>Special: Subject = own superweapon building, Cell = target.</summary>
    private void ApplySpecial(Command c)
    {
        // M3-13 fills this
    }

    /// <summary>Per tick (last in RunEffects): charge superweapon buildings.</summary>
    private void RunSuperweaponCharge()
    {
        // M3-13 fills this
    }
}
