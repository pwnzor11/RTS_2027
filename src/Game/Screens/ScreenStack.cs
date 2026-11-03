// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Input;

namespace Rts.Game.Screens;

/// <summary>
/// The stack of screens. Updates run top-down until a screen blocks updates below it; drawing runs bottom-up starting
/// at the lowest screen that is still visible. Push, pop and replace calls made while the stack is updating (by a
/// screen reacting to input) are queued and applied when the update pass ends, so a screen never changes the list it is
/// being iterated from and a screen pushed this frame does not see this frame's input.
/// </summary>
public sealed class ScreenStack
{
    private readonly List<IScreen> _screens = new();
    private readonly Queue<Action> _deferred = new();
    private bool _updating;

    /// <summary>Number of screens on the stack.</summary>
    public int Count => _screens.Count;

    /// <summary>The top screen, or null when empty.</summary>
    public IScreen? Top => _screens.Count == 0 ? null : _screens[^1];

    /// <summary>The screens bottom to top.</summary>
    public IReadOnlyList<IScreen> Screens => _screens;

    /// <summary>Puts a screen on top and calls its <see cref="IScreen.Enter"/>.</summary>
    public void Push(IScreen screen) => Run(() =>
    {
        _screens.Add(screen);
        screen.Enter();
    });

    /// <summary>Removes the top screen (calling <see cref="IScreen.Exit"/>). Does nothing when empty.</summary>
    public void Pop() => Run(PopNow);

    /// <summary>Replaces the top screen with <paramref name="screen"/> (exit old, enter new).</summary>
    public void Replace(IScreen screen) => Run(() =>
    {
        PopNow();
        _screens.Add(screen);
        screen.Enter();
    });

    /// <summary>Removes every screen top-down, then puts <paramref name="screen"/> on the empty stack.</summary>
    public void ReplaceAll(IScreen screen) => Run(() =>
    {
        ClearNow();
        _screens.Add(screen);
        screen.Enter();
    });

    /// <summary>Removes every screen top-down (used at shutdown so each gets its <see cref="IScreen.Exit"/>).</summary>
    public void Clear() => Run(ClearNow);

    /// <summary>
    /// Runs <paramref name="action"/> now, or after the current update pass when called from inside one. Use it for
    /// a sequence of stack changes that must happen in order (for example pop twice, then push).
    /// </summary>
    public void Post(Action action) => Run(action);

    /// <summary>Updates the screens top-down; stops after the first one whose <see cref="IScreen.BlocksUpdateBelow"/> is true.</summary>
    public void Update(InputFrame input, GameTime t)
    {
        _updating = true;
        try
        {
            for (int i = _screens.Count - 1; i >= 0; i--)
            {
                IScreen screen = _screens[i];
                screen.Update(input, t);
                if (screen.BlocksUpdateBelow) break;
            }
        }
        finally
        {
            _updating = false;
        }

        while (_deferred.Count > 0) _deferred.Dequeue()();
    }

    /// <summary>Draws bottom-up, starting at the lowest screen not hidden by an opaque screen above it.</summary>
    public void Draw(SpriteBatch sb)
    {
        int first = _screens.Count - 1;
        while (first > 0 && !_screens[first].BlocksDrawBelow) first--;
        for (int i = Math.Max(first, 0); i < _screens.Count; i++) _screens[i].Draw(sb);
    }

    private void Run(Action action)
    {
        if (_updating) _deferred.Enqueue(action);
        else action();
    }

    private void PopNow()
    {
        if (_screens.Count == 0) return;
        IScreen top = _screens[^1];
        _screens.RemoveAt(_screens.Count - 1);
        top.Exit();
    }

    private void ClearNow()
    {
        while (_screens.Count > 0) PopNow();
    }
}
