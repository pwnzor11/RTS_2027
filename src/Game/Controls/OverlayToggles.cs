// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Controls;

/// <summary>Which debug overlays are on. Toggled with F3, F4 and F5; the HUD lists the ones that are on.</summary>
public sealed class OverlayToggles
{
    /// <summary>F3: occupancy tints on cells (vehicle blue, infantry green, building red).</summary>
    public bool Occupancy { get; set; }

    /// <summary>F4: remaining path and reserved cell of each selected unit.</summary>
    public bool Paths { get; set; }

    /// <summary>F5: weapon range (and minimum range) circles of each selected unit.</summary>
    public bool Ranges { get; set; }

    /// <summary>Short text for the HUD, such as "F3 occupancy F5 ranges", or "no overlays".</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Occupancy) parts.Add("F3 occupancy");
        if (Paths) parts.Add("F4 paths");
        if (Ranges) parts.Add("F5 ranges");
        return parts.Count == 0 ? "no overlays" : string.Join(" ", parts);
    }
}
