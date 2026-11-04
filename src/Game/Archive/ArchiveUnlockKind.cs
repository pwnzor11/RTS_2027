// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>How an Archive entry becomes readable.</summary>
public enum ArchiveUnlockKind
{
    /// <summary>Always readable.</summary>
    Open,

    /// <summary>Readable once a campaign mission is completed.</summary>
    Campaign,

    /// <summary>Readable once the player has seen the rules type in play.</summary>
    Encounter,

    /// <summary>Readable once the player has finished researching the upgrade.</summary>
    Research,
}
