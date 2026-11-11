// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>A screen that can ask for its pause menu when the window loses focus (a single-player game).</summary>
public interface IFocusPausable
{
    /// <summary>
    /// The pause menu to open now because focus was lost, or null when this screen must not pause (multiplayer, spectating, the sandbox,
    /// or the player already paused).
    /// </summary>
    IScreen? CreateFocusPause();
}

/// <summary>
/// Pauses a single-player game when the window loses focus and resumes it when focus returns, but only if this class did the
/// pausing: a game the player had paused (P or the pause menu) stays paused (S18 GAMEREND-05, D-205). Called once per frame, outside the stack's update.
/// </summary>
public sealed class FocusPause
{
    private bool _wasActive = true;
    private IScreen? _ours;

    /// <summary>Feeds the focus state of this frame.</summary>
    public void Update(bool active, ScreenStack stack)
    {
        if (!active && _wasActive)
        {
            if (stack.Top is IFocusPausable game && game.CreateFocusPause() is { } pause)
            {
                _ours = pause;
                stack.Push(pause);
            }
        }
        else if (active && !_wasActive)
        {
            if (_ours is not null && ReferenceEquals(stack.Top, _ours)) stack.Pop();
            _ours = null;
        }

        _wasActive = active;
    }
}
