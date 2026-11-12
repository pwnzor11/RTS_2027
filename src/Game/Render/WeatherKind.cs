// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>Which weather particles a map shows (M10-12). View only.</summary>
public enum WeatherKind
{
    /// <summary>No weather.</summary>
    None = 0,
    /// <summary>Ashfall (Ash Plains).</summary>
    Ash = 1,
    /// <summary>Wind-blown glass dust (Glass Coast).</summary>
    Glass = 2,
    /// <summary>Snow (mountains and other cold maps).</summary>
    Snow = 3,
    /// <summary>Rain (Rust Belt).</summary>
    Rain = 4,
}
