// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Controls;

namespace Rts.Game.Ui.Sidebar;

/// <summary>
/// Keyboard hotkeys for the cameo grid (M13-15, D-205). Twelve rebindable actions map to the grid positions of the active tab: three
/// columns by four cameo rows, counted from the top of what is on screen (tier headings are skipped). The default keys are the number pad,
/// laid out like the grid (7 8 9 / 4 5 6 / 1 2 3 / 0 . +) because every letter that makes a good grid is already an order key; rebind them
/// on the Controls page (Q W E R / A S D F / Z X C V works once the order keys are moved).
/// </summary>
public static class CameoHotkeys
{
    /// <summary>Columns of hotkey positions.</summary>
    public const int Columns = 3;

    /// <summary>Cameo rows of hotkey positions.</summary>
    public const int Rows = 4;

    /// <summary>Number of cameo hotkeys.</summary>
    public const int Count = Columns * Rows;

    /// <summary>The actions in grid order (row by row, left to right); the list lives in <see cref="SidebarPanel.CameoActions"/>, where the input code reads them.</summary>
    public static IReadOnlyList<GameAction> Actions => SidebarPanel.CameoActions;

    /// <summary>The hotkey position of a (cameo row, column), or -1 when it has no key.</summary>
    public static int IndexOf(int cameoRow, int column) =>
        cameoRow >= 0 && cameoRow < Rows && column >= 0 && column < Columns ? cameoRow * Columns + column : -1;

    /// <summary>
    /// The entry at hotkey position <paramref name="index"/> of <paramref name="rows"/> (the rows on screen, in order). Heading rows do not
    /// count; null when the position holds no cameo.
    /// </summary>
    public static TierEntry? EntryAt(IReadOnlyList<TierRow> rows, int index)
    {
        if (index < 0 || index >= Count) return null;
        int wantRow = index / Columns, wantCol = index % Columns, cameoRow = 0;
        foreach (TierRow row in rows)
        {
            if (row.Heading) continue;
            if (cameoRow == wantRow) return wantCol < row.Cells.Count ? row.Cells[wantCol] : null;
            cameoRow++;
        }

        return null;
    }

    /// <summary>The short name of the key bound to position <paramref name="index"/> ("Num 7").</summary>
    public static string KeyName(int index) => KeyNames.Of(GameKeys.Active.Get(Actions[index]));
}
