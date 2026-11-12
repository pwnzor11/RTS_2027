// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>The scroll position of the credits roll (M10-20), in quarter pixels so the speed can be gentle. Lines enter from the bottom and leave at the top.</summary>
public sealed class CreditsRoll
{
    /// <summary>Quarter pixels the roll moves each update (about 30 pixels a second at 60 updates).</summary>
    public const int QuartersPerUpdate = 2;

    private int _quarters;

    /// <summary>How far the roll has moved, in whole pixels.</summary>
    public int Offset => _quarters / 4;

    /// <summary>Moves the roll on by <paramref name="updates"/> updates.</summary>
    public void Advance(int updates = 1) => _quarters += QuartersPerUpdate * Math.Max(0, updates);

    /// <summary>The window y of line <paramref name="index"/> when lines are <paramref name="lineHeight"/> tall in a window <paramref name="viewHeight"/> tall.</summary>
    public int LineY(int index, int lineHeight, int viewHeight) => viewHeight + index * lineHeight - Offset;

    /// <summary>True once the last of <paramref name="lineCount"/> lines has left the top of the window.</summary>
    public bool IsFinished(int lineCount, int lineHeight, int viewHeight) => LineY(lineCount, lineHeight, viewHeight) < 0;
}
