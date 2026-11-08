// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>The procedural outline a decal is drawn with (M12-01). Names are the <c>shape</c> values in the theater files.</summary>
public enum DecalShape
{
    /// <summary>A hard-edged irregular disc: pothole, pool, oil spot.</summary>
    Blob,

    /// <summary>A disc whose opacity falls off from the centre: soot, snow, ash drift, salt crust.</summary>
    Soft,

    /// <summary>A crater: dark inside, lit rim on the north-west half, shaded rim on the south-east half.</summary>
    Ring,

    /// <summary>A jagged one-texel line running both ways from the centre: cracks.</summary>
    Crack,

    /// <summary>A scatter of one- or two-texel dots: pebbles, scree, shells, bones.</summary>
    Dots,

    /// <summary>Three wavy lines: sand ripples.</summary>
    Ripple,

    /// <summary>A dashed bar: a drowned road stripe.</summary>
    Stripe,
}
