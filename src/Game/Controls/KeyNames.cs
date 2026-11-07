// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>Player-facing names for keyboard keys.</summary>
public static class KeyNames
{
    /// <summary>The key as shown on screen, for example <c>.</c> for OemPeriod and <c>1</c> for D1.</summary>
    public static string Of(Keys key) => key switch
    {
        Keys.OemPeriod => ".",
        Keys.OemComma => ",",
        Keys.OemMinus => "-",
        Keys.OemPlus => "=",
        Keys.OemQuestion => "/",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.OemTilde => "`",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe => "\\",
        Keys.Space => "Space",
        >= Keys.D0 and <= Keys.D9 => ((int)key - (int)Keys.D0).ToString(System.Globalization.CultureInfo.InvariantCulture),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + ((int)key - (int)Keys.NumPad0).ToString(System.Globalization.CultureInfo.InvariantCulture),
        Keys.PageUp => "Page Up",
        Keys.PageDown => "Page Down",
        _ => key.ToString(),
    };
}
