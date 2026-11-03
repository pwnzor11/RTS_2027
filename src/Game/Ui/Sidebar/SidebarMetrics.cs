// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Sidebar;

/// <summary>Small pure helpers for the credits counter and the power bar.</summary>
public static class SidebarMetrics
{
    /// <summary>
    /// One animation step of the credits readout: moves <paramref name="shown"/> toward <paramref name="target"/> by an eighth
    /// of the gap (at least 1), never overshooting.
    /// </summary>
    public static int CreditStep(int shown, int target)
    {
        int diff = target - shown;
        if (diff == 0) return shown;
        int step = Math.Max(1, Math.Abs(diff) / 8);
        return diff > 0 ? shown + Math.Min(step, diff) : shown - Math.Min(step, -diff);
    }

    /// <summary>
    /// Power bar geometry for a bar <paramref name="heightPx"/> tall: the scale is the larger of output and use (at least 1), so the
    /// filled part is output and the marker is use. <c>Low</c> is true when use exceeds output (drawn red).
    /// </summary>
    public static (int FillPx, int UsePx, bool Low) PowerBar(int powerOut, int powerUse, int heightPx)
    {
        int scale = Math.Max(1, Math.Max(powerOut, powerUse));
        int fill = (int)((long)Math.Max(0, powerOut) * heightPx / scale);
        int use = (int)((long)Math.Max(0, powerUse) * heightPx / scale);
        return (fill, use, powerUse > powerOut);
    }
}
