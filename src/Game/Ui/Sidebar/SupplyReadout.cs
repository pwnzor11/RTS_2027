// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Game.Ui.Sidebar;

/// <summary>Colour band of the supply counter (SUP-4, design 13 §5).</summary>
public enum SupplyTone : byte
{
    /// <summary>Plenty of room under the cap.</summary>
    Normal = 0,

    /// <summary>Within <see cref="SupplyReadout.AmberMargin"/> of the cap.</summary>
    Amber = 1,

    /// <summary>At or over the cap: production of supply-costing units waits.</summary>
    Red = 2,
}

/// <summary>
/// Pure rules of the sidebar supply counter (SUP-4, docs/design/13_supply_cap.md §5): when it is shown, its colour band and its text.
/// The sim decides every number; this only reads them. Integer arithmetic only, so it is unit-tested without graphics.
/// </summary>
public static class SupplyReadout
{
    /// <summary>The counter turns amber when used is at least this far below the cap (design 13 §5: cap - 4).</summary>
    public const int AmberMargin = 4;

    /// <summary>The counter is shown only while the supply rule is on and the player has some cap (a dead conyard leaves none).</summary>
    /// <param name="rulesEnabled">The game's <c>[supply] enabled</c> switch.</param>
    /// <param name="supply">The player's supply from the HUD.</param>
    public static bool Shown(bool rulesEnabled, SupplyView supply) => rulesEnabled && supply.Cap > 0;

    /// <summary>Colour band for used and cap: red at or over the cap, amber within <see cref="AmberMargin"/> of it, else normal.</summary>
    public static SupplyTone ToneOf(int used, int cap)
    {
        if (used >= cap) return SupplyTone.Red;
        if (used >= cap - AmberMargin) return SupplyTone.Amber;
        return SupplyTone.Normal;
    }

    /// <summary>The counter text, "used/cap" (for example "34/60").</summary>
    public static string Text(SupplyView supply) =>
        supply.Used.ToString(CultureInfo.InvariantCulture) + "/" + supply.Cap.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// True when the head of a unit lane is paused for supply (SUP-4): the rule is on, the unit costs supply and used + its supply is over
    /// the cap. Mirrors the sim's lane pause (queued items do not count against the cap; only used does).
    /// </summary>
    /// <param name="rulesEnabled">The game's <c>[supply] enabled</c> switch.</param>
    /// <param name="used">Supply in use (HUD).</param>
    /// <param name="cap">Supply cap (HUD).</param>
    /// <param name="headSupply">What the head unit costs in supply (<c>Simulation.SupplyOfType</c>), 0 for free units.</param>
    public static bool HeadWaitsForSupply(bool rulesEnabled, int used, int cap, int headSupply) =>
        rulesEnabled && headSupply > 0 && used + headSupply > cap;
}
