// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;

namespace Rts.Game.Lan;

/// <summary>
/// A43-QOL-09: a map ping travels as a chat-like team message, never as a sim command (so it cannot touch the sim state, the replay or
/// the lockstep). The text starts with a control character the chat box cannot type, so ordinary chat can never be taken for a ping;
/// the rest is the cell as <c>x,y</c>. The session strips pings out of the chat lines and hands them to the screen.
/// </summary>
public static class LanPing
{
    /// <summary>First character of a ping message (SOH, not typeable into the one-line chat input).</summary>
    public const char Prefix = '\u0001';

    /// <summary>The wire text of a ping at <paramref name="cell"/>.</summary>
    public static string Encode(CellCoord cell) => string.Create(CultureInfo.InvariantCulture, $"{Prefix}{cell.X},{cell.Y}");

    /// <summary>True when <paramref name="text"/> is a ping; <paramref name="cell"/> is then its cell. Malformed text is not a ping.</summary>
    public static bool TryDecode(string? text, out CellCoord cell)
    {
        cell = default;
        if (string.IsNullOrEmpty(text) || text[0] != Prefix) return false;
        int comma = text.IndexOf(',');
        if (comma < 2 || comma >= text.Length - 1) return false;
        if (!int.TryParse(text.AsSpan(1, comma - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int x)) return false;
        if (!int.TryParse(text.AsSpan(comma + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int y)) return false;
        if (x > 4096 || y > 4096) return false;
        cell = new CellCoord(x, y);
        return true;
    }

    /// <summary>True when the text is a ping (even one with a bad cell), so it never shows as a chat line.</summary>
    public static bool IsPing(string? text) => !string.IsNullOrEmpty(text) && text[0] == Prefix;
}
