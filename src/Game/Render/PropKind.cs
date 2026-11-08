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
}
