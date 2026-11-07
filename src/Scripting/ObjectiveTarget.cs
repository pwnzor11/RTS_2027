// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>What an objective's location refers to (M11-05).</summary>
public enum ObjectiveTargetKind : byte
{
    /// <summary>The objective has no location.</summary>
    None = 0,

    /// <summary>A fixed cell (<see cref="ObjectiveTarget.X"/>, <see cref="ObjectiveTarget.Y"/>).</summary>
    Cell = 1,

    /// <summary>A mission zone, by name; the target is the zone's middle cell.</summary>
    Zone = 2,

    /// <summary>An actor tag; the target follows the actor while it is alive.</summary>
    Actor = 3,
}

/// <summary>
/// An optional location for a mission objective (<c>Objective.SetTarget</c>, M11-05). Saved and hashed with the mission part
/// (format 5 tail, written only when some objective has a target). Integers and names only.
/// </summary>
/// <param name="Kind">What the target is.</param>
/// <param name="Name">Zone name or actor tag; empty for a cell.</param>
/// <param name="X">Cell X for a <see cref="ObjectiveTargetKind.Cell"/>, else 0.</param>
/// <param name="Y">Cell Y for a <see cref="ObjectiveTargetKind.Cell"/>, else 0.</param>
public readonly record struct ObjectiveTarget(ObjectiveTargetKind Kind, string Name = "", int X = 0, int Y = 0)
{
    /// <summary>True when the objective has a location.</summary>
    public bool IsSet => Kind != ObjectiveTargetKind.None;
}
