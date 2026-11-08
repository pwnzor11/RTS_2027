// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>A land-linked sprite kind (M12-02). Names are the <c>rock</c>, <c>rough</c> and <c>bank</c> values in the theater files. Append only.</summary>
public enum PropKind
{
    /// <summary>A faceted grey boulder, for Rock clusters.</summary>
    Boulder,

    /// <summary>A pile of broken brick and rebar, for Rough clusters.</summary>
    Rubble,

    /// <summary>A cluster of fused-glass spikes, for dune Rock on the Glass Coast.</summary>
    GlassCrest,

    /// <summary>A tilted slab of ice, for river banks.</summary>
    IceSlab,

    /// <summary>M12-04: a stripped dead tree. A blocking prop (<see cref="Rts.Sim.Rules.OverlayKind.Tree"/>).</summary>
    DeadTree,

    /// <summary>M12-04: a wrecked car, truck or tank hull (<see cref="Rts.Sim.Rules.OverlayKind.Wreck"/>).</summary>
    WreckHulk,

    /// <summary>M12-04: a concrete barrier or block (<see cref="Rts.Sim.Rules.OverlayKind.Block"/>).</summary>
    ConcreteBlock,

    /// <summary>M12-04: a broken pylon (<see cref="Rts.Sim.Rules.OverlayKind.Pylon"/>).</summary>
    BrokenPylon,

    /// <summary>M12-04: driftwood or debris (<see cref="Rts.Sim.Rules.OverlayKind.Snag"/>).</summary>
    Snag,
}
