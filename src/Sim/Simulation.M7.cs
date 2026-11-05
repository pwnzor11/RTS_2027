// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Beachhead;
using Rts.Sim.Naval;
using Rts.Sim.Shipments;

namespace Rts.Sim;

// M7-02 (docs/design/11_m7_plan.md §2): contracts of The Overseas Expedition. Accessors for the "shipments", "beachhead" and "naval"
// parts. No behaviour yet: the parts stay empty until M7-03..07.
public sealed partial class Simulation
{
    /// <summary>The "shipments" part (M7-02): shipment timers, decay steps, charter progress and tenders in flight per player.</summary>
    public ShipmentState Shipments => _shipments;

    /// <summary>The "beachhead" part (M7-02): the per-player build-zone mask.</summary>
    public BeachheadState Beachhead => _beachhead;

    /// <summary>The "naval" part (M7-02): ship region labels, landing-craft unload queues and submerged flags.</summary>
    public NavalState Naval => _naval;
}
