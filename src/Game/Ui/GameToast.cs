// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>
/// One short line shown for a few seconds over the map (D-205): "Quicksaved.", the Expedition start hint, a failed save. A new line replaces
/// the old one. Time is the screen's own clock in milliseconds, so the rules are testable without a window.
/// </summary>
public sealed class GameToast
{
    /// <summary>How long a line stays, in milliseconds, unless <see cref="Show"/> is given another time.</summary>
    public const int DefaultMs = 2500;

    private string _text = "";
    private long _untilMs;

    /// <summary>Shows <paramref name="text"/> from <paramref name="nowMs"/> for <paramref name="durationMs"/>. Empty text clears the toast.</summary>
    public void Show(string text, long nowMs, int durationMs = DefaultMs)
    {
        _text = text;
        _untilMs = nowMs + durationMs;
    }

    /// <summary>The line to draw at <paramref name="nowMs"/>, or null when there is none (or it has timed out).</summary>
    public string? Current(long nowMs) => _text.Length > 0 && nowMs < _untilMs ? _text : null;
}
