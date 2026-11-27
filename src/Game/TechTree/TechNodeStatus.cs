// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.TechTree;

/// <summary>How a tech-tree node stands for the viewing player (U-12).</summary>
public enum TechNodeStatus
{
    /// <summary>Not buildable yet: a prerequisite is missing.</summary>
    Locked = 0,

    /// <summary>Locked by the mission's tech gating (campaign), whatever the prerequisites say.</summary>
    MissionLocked = 1,

    /// <summary>Unlocked: the player may queue it now.</summary>
    Available = 2,

    /// <summary>A structure the player owns, or an upgrade already researched.</summary>
    Built = 3,
}
