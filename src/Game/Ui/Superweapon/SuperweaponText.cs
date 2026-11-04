// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Superweapon;

/// <summary>
/// Text keys of the superweapon UI (M3-21) with their PLACEHOLDER English text. There is no string table yet; when one lands, the keys
/// below are what to register and <see cref="Placeholder"/> is the one place to swap.
/// </summary>
public static class SuperweaponText
{
    /// <summary>Short HUD message shown for a few seconds when a superweapon finishes charging. Argument 0 = the button caption.</summary>
    public const string ReadyKey = "hud.superweapon.ready";

    /// <summary>Placeholder text for a key, with the caption (for example "SUNRISE") filled in. Unknown keys return the key.</summary>
    public static string Placeholder(string key, string caption) =>
        key == ReadyKey ? $"{caption} READY - click it, then pick a target" : key;
}
