// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>Which side an Archive entry belongs to (drives the colour theme of the reading pane).</summary>
public enum ArchiveFaction
{
    /// <summary>Neither side (shared history, technology).</summary>
    Neutral,

    /// <summary>The Continental Reconstruction Authority.</summary>
    Authority,

    /// <summary>The Directorate of the Interior.</summary>
    Directorate,

    /// <summary>The Left Behind, the caravans of the Glass Coast.</summary>
    LeftBehind,

    /// <summary>The Overseas Expedition, the Tidewater Concordat's fleet.</summary>
    Expedition,
}
