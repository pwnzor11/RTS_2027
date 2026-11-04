// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>The tab an Archive entry lives under.</summary>
public enum ArchiveCategory
{
    /// <summary>World and faction history.</summary>
    History,

    /// <summary>A faction's identity and how it plays.</summary>
    Faction,

    /// <summary>A named leader or anchor character.</summary>
    Leader,

    /// <summary>A mobile unit (infantry or vehicle); stats come from the rules.</summary>
    Unit,

    /// <summary>A building; stats come from the rules.</summary>
    Structure,

    /// <summary>A researchable upgrade; stats come from the rules.</summary>
    Upgrade,

    /// <summary>A piece of technology or a world mechanic (ashfall, tunnels, ...).</summary>
    Technology,
}
