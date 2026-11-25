// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Ui;

/// <summary>
/// The hover panel text for a unit or building under the mouse (QOL-06): name, owner, health. Pure, so it is unit-tested. The
/// caller passes only what the viewer's snapshot holds (fog already applied); a last-seen enemy building says so.
/// </summary>
public static class HoverInfo
{
    /// <summary>How long the mouse rests on the same thing before the panel opens, in milliseconds.</summary>
    public const int DwellMs = 500;

    /// <summary>The panel lines: name, owner, and health (or the last-seen health of a ghost building).</summary>
    public static List<string> Lines(string name, string owner, int health, int maxHealth, bool lastSeen)
    {
        string hp = maxHealth > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{health}/{maxHealth}")
            : string.Create(CultureInfo.InvariantCulture, $"{health}");
        return new List<string>
        {
            name,
            owner,
            lastSeen ? "Last seen, health " + hp : "Health " + hp,
        };
    }

    /// <summary>The owner line: "Owner: you" for the local player, otherwise "Owner: player N" (1-based).</summary>
    public static string OwnerLine(byte owner, byte local) =>
        owner == local ? "Owner: you" : string.Create(CultureInfo.InvariantCulture, $"Owner: player {owner + 1}");
}
