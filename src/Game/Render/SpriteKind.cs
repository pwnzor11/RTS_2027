// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>What a sprite is drawn for. Decides the placeholder shape and which rules table names it.</summary>
public enum SpriteKind
{
    /// <summary>A structure, drawn at its footprint's top-left origin.</summary>
    Building,

    /// <summary>A vehicle, drawn on its ground contact point, 16 facings.</summary>
    Vehicle,

    /// <summary>An infantry unit, drawn on its ground contact point, 8 facings.</summary>
    Infantry,
}
