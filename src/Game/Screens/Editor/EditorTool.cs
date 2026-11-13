// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens.Editor;

/// <summary>The map editor's tools (design 08 §5).</summary>
public enum EditorTool
{
    /// <summary>Paint a land type with the brush.</summary>
    Land,
    /// <summary>Paint a height level 0..3 with the brush.</summary>
    Height,
    /// <summary>Paint Ashfall or Pitchblende with a density 1..12.</summary>
    Resource,
    /// <summary>Place a neutral feature (a ruin).</summary>
    Feature,
    /// <summary>Place a numbered start position (and its symmetric images).</summary>
    Start,
    /// <summary>Clear resources, props, decals, features and starts under the brush.</summary>
    Eraser,
    /// <summary>Flood-fill a land type.</summary>
    Fill,
    /// <summary>Paint a blocking prop (tree, wreck, block, pylon, snag) with the brush.</summary>
    Props,
    /// <summary>Paint a decal kind (0 = erase decals) with the brush.</summary>
    Decal,
}
