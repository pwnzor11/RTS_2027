// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Saves;

/// <summary>What kind of game a save holds. Values are stored in the file: append only.</summary>
public enum SaveMode : byte
{
    /// <summary>A menu skirmish.</summary>
    Skirmish = 1,
    /// <summary>A campaign mission (the script state is inside the simulation bytes).</summary>
    Mission = 2,
}
