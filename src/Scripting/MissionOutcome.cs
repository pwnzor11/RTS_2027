// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>How the script ended the mission (Mission.Win / Mission.Lose).</summary>
public enum MissionOutcome : byte
{
    /// <summary>Still running.</summary>
    None = 0,

    /// <summary>The script called Mission.Win.</summary>
    Won = 1,

    /// <summary>The script called Mission.Lose.</summary>
    Lost = 2,
}
