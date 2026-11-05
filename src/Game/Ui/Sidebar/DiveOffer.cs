// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Sidebar;

/// <summary>M7-14: caption of the Dive/Surface button for a selected own ship with <c>submerges</c>. The click sends Deploy, which toggles (D-143).</summary>
public static class DiveOffer
{
    /// <summary>"Dive (D)" for a surfaced ship, "Surface (D)" for a dived one.</summary>
    public static string Caption(bool submerged) => UiText.Get(submerged ? UiText.SurfaceCaption : UiText.DiveCaption);
}
