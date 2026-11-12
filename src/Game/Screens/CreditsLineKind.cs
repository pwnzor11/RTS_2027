// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>How a line of the credits roll is drawn (M10-20).</summary>
public enum CreditsLineKind
{
    /// <summary>The big title.</summary>
    Title,
    /// <summary>A section heading.</summary>
    Heading,
    /// <summary>A credited name.</summary>
    Name,
    /// <summary>Plain text under a name.</summary>
    Text,
    /// <summary>An empty line.</summary>
    Blank,
}
