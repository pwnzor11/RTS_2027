// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Alerts;

/// <summary>
/// The last-alert key: the first press goes to the most recent alert with a place, each further press steps back through the last few
/// and wraps round. A new alert, or a pause of more than <see cref="ResetTicks"/> sim ticks, starts over at the newest.
/// </summary>
public sealed class AlertJumpCursor
{
    /// <summary>Ticks after the last press at which the cursor starts over (6 s).</summary>
    public const int ResetTicks = 6 * AlertInfo.TicksPerSecond;

    private int _index = -1;
    private int _version = -1;
    private int _lastPress = int.MinValue;

    /// <summary>The place to go to for a press at <paramref name="tick"/>, or null when there is no alert with a place yet.</summary>
    public LeptonCoord? Press(AlertLog log, int tick)
    {
        IReadOnlyList<JumpTarget> targets = log.JumpTargets();
        if (targets.Count == 0) return null;
        bool fresh = _index < 0 || _version != log.JumpVersion || tick - _lastPress > ResetTicks || tick < _lastPress;
        _index = fresh ? 0 : (_index + 1) % targets.Count;
        _version = log.JumpVersion;
        _lastPress = tick;
        return targets[_index].Pos;
    }
}
