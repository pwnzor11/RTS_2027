// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework.Input;

namespace Rts.Game.Controls;

/// <summary>
/// A bindable input: one key, optionally with Alt held (M16 U-06, Q-91). Alt is the only bindable modifier: Shift and Ctrl keep their fixed
/// meanings (queue five / add to group, set group), so chord matching ignores them.
/// </summary>
/// <param name="Key">The key; <see cref="Keys.None"/> means no binding.</param>
/// <param name="Alt">True when Alt must be held.</param>
public readonly record struct KeyChord(Keys Key, bool Alt)
{
    private const string AltPrefix = "Alt+";

    /// <summary>The chord for a bare key (no Alt).</summary>
    public static KeyChord Of(Keys key) => new(key, false);

    /// <summary>No binding.</summary>
    public static readonly KeyChord None = new(Keys.None, false);

    /// <summary>True when there is no key.</summary>
    public bool IsNone => Key == Keys.None;

    /// <summary>The settings-file text: <c>D1</c> or <c>Alt+D1</c>.</summary>
    public string ToSetting() => Alt ? AltPrefix + Key : Key.ToString();

    /// <summary>Parses settings text (case-insensitive, optional <c>Alt+</c> prefix). False for an unknown key name.</summary>
    public static bool TryParse(string? s, out KeyChord chord)
    {
        chord = None;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        bool alt = s.StartsWith(AltPrefix, StringComparison.OrdinalIgnoreCase);
        if (alt) s = s[AltPrefix.Length..].Trim();
        if (s.Length == 0 || char.IsDigit(s[0])) return false;   // a bare number would parse as the Keys value
        if (!Enum.TryParse(s, ignoreCase: true, out Keys key) || !Enum.IsDefined(key)) return false;
        chord = new KeyChord(key, alt);
        return true;
    }
}
