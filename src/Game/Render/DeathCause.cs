// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>What killed a soldier, as far as the death animation cares (M10-14b).</summary>
public enum DeathCause : byte
{
    /// <summary>Not known: the plain "die" state.</summary>
    Unknown = 0,
    /// <summary>An explosive hit: "die_blast".</summary>
    Blast = 1,
    /// <summary>Fire or an incendiary hit: "die_burn".</summary>
    Burn = 2,
    /// <summary>Radiation or ash: "die_rad".</summary>
    Rad = 3,
}
