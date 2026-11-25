// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;

namespace Rts.Game.Ui.Sidebar;

/// <summary>M7-14: caption of the Dive/Surface button for a selected own ship with <c>submerges</c>. The click sends Deploy, which toggles (D-143).</summary>
public static class DiveOffer
{
    /// <summary>The Deploy key as the player has it bound now (U-01: the caption shows the current letter, "D" by default).</summary>
    public static string DeployKey => KeyNames.Of(GameKeys.Active.Get(GameAction.Deploy));

    /// <summary>"Dive (D)" for a surfaced ship, "Surface (D)" for a dived one.</summary>
    public static string Caption(bool submerged) => UiText.Get(submerged ? UiText.SurfaceCaption : UiText.DiveCaption).Replace("{key}", DeployKey, StringComparison.Ordinal);

    /// <summary>S44 M-15: caption of the Drop beacon button; <paramref name="armed"/> = waiting for the target cell.</summary>
    public static string BeaconCaption(bool armed) => UiText.Get(armed ? UiText.BeaconArmedCaption : UiText.BeaconCaption);

    /// <summary>S44 M-01: caption of the same button for a selected minelayer: "Lay mine (D) 3 left".</summary>
    public static string MineCaption(int stock) => UiText.Get(UiText.MineCaption).Replace("{key}", DeployKey, StringComparison.Ordinal).Replace("{stock}", stock.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
