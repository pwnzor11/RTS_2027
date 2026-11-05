// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rigs;
using Rts.Sim.Salvage;

namespace Rts.Sim;

// M6-02 (docs/design/10_m6_plan.md §3): contracts of The Left Behind. Accessors for the "rigs" and "salvage" parts. No behaviour yet:
// CommandKind.Pack is accepted and ignored until M6-03; the Salvage/Pack/Withdraw missions run the idle handler until M6-03..05.
public sealed partial class Simulation
{
    /// <summary>The "rigs" part (M6-02): pack state per rig building and per packed rig vehicle.</summary>
    public RigState Rigs => _rigs;

    /// <summary>The "salvage" part (M6-02): the wreck table.</summary>
    public SalvageState Salvage => _salvage;
}
