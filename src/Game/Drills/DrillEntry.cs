// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Profile;
using Rts.Scripting;

namespace Rts.Game.Drills;

/// <summary>One row of the Drills list: the mission, its thresholds and the player's best result.</summary>
/// <param name="Id">Mission folder name.</param>
/// <param name="Title">Mission title.</param>
/// <param name="Def">The medal thresholds.</param>
/// <param name="Record">The best result so far (default = never won).</param>
public sealed record DrillEntry(string Id, string Title, DrillDef Def, DrillRecord Record)
{
    /// <summary>The menu row text.</summary>
    public string Label => Title + "   " + DrillText.Best(Record);
}
