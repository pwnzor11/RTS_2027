// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;

namespace Rts.Game.Ui;

/// <summary>U-02: the toast lines for selection and control-group keys, so a key that does nothing (or sets a group) says so.</summary>
public static class SelectionFeedbackText
{
    /// <summary>The select-army key found no combat unit; the selection is kept.</summary>
    public static string NoArmy() => UiText.Get(UiText.NoArmySelection);

    /// <summary>Ctrl+number set group <paramref name="number"/> to <paramref name="count"/> units or structures: "Group 3 set (5 units)".</summary>
    public static string GroupSet(int number, int count)
    {
        string n = number.ToString(CultureInfo.InvariantCulture);
        return count == 1
            ? UiText.Format(UiText.GroupSetOneToast, "number", n)
            : UiText.Format(UiText.GroupSetToast, "number", n).Replace("{count}", count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }
}
