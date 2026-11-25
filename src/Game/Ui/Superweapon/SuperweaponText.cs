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

    /// <summary>A43 GAMEUI-03: shown when a targeted click is refused because the cell is outside the entrance range.</summary>
    public const string OutOfRangeKey = "hud.superweapon.out_of_range";

    /// <summary>A43 GAMEUI-03: shown when a targeted click is refused for another reason the sim gives.</summary>
    public const string CannotFireKey = "hud.superweapon.cannot_fire";

    /// <summary>Placeholder text for a key, with the caption (for example "SUNRISE") filled in. Unknown keys return the key.</summary>
    public static string Placeholder(string key, string caption) => key switch
    {
        ReadyKey => $"{caption} READY - click it, then pick a target",
        OutOfRangeKey => "Out of range",
        CannotFireKey => "Invalid target",
        _ => key,
    };
}
