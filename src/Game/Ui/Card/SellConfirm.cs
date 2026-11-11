// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Card;

/// <summary>
/// The card's Sell button needs two clicks (D-205, S18 GAMEUI-06): the first arms it for <see cref="WindowMs"/> milliseconds (the button
/// then reads "Confirm"), a second click on the same building inside that window sells, and an arm that runs out or is for another
/// building does nothing. Time is the screen's clock in milliseconds, so the rule is testable.
/// </summary>
public sealed class SellConfirm
{
    /// <summary>How long the first click stays armed, in milliseconds.</summary>
    public const int WindowMs = 2000;

    private EntityId _building = EntityId.None;
    private long _untilMs;

    /// <summary>True while a click on <paramref name="building"/> would sell it (armed and not timed out).</summary>
    public bool IsArmed(EntityId building, long nowMs) => !building.IsNone && building == _building && nowMs < _untilMs;

    /// <summary>
    /// A click on Sell. Returns true when this click sells (it was the second one in time); otherwise arms the button for
    /// <paramref name="building"/> and returns false.
    /// </summary>
    public bool Click(EntityId building, long nowMs)
    {
        if (IsArmed(building, nowMs))
        {
            Reset();
            return true;
        }

        _building = building;
        _untilMs = nowMs + WindowMs;
        return false;
    }

    /// <summary>Disarms (another building was selected, or the selection went away).</summary>
    public void Reset()
    {
        _building = EntityId.None;
        _untilMs = 0;
    }
}
