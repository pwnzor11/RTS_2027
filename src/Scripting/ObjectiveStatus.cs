// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>State of a mission objective.</summary>
public enum ObjectiveStatus : byte
{
    /// <summary>Not finished.</summary>
    Active = 0,

    /// <summary>Objective.Complete was called.</summary>
    Completed = 1,

    /// <summary>Objective.Fail was called.</summary>
    Failed = 2,
}
