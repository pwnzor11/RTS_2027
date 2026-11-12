// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>The buttons of the LAN overlay (M9-08, M9-10).</summary>
public enum LanOverlayActionKind
{
    /// <summary>Host: drop the lagging peer.</summary>
    Drop,

    /// <summary>Start the resume countdown.</summary>
    Resume,

    /// <summary>Host, desync: resync every client from the host's save.</summary>
    Resync,

    /// <summary>Host, desync: keep playing.</summary>
    Continue,

    /// <summary>Host, desync: end the match.</summary>
    EndMatch,

    /// <summary>Client, host gone: continue against AI from a skirmish save.</summary>
    ContinueAgainstAi,

    /// <summary>Back to the main menu.</summary>
    BackToMenu,
}
