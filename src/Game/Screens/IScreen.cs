// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Input;

namespace Rts.Game.Screens;

/// <summary>One full-window state of the game (menu, play, pause, game over) living on a <see cref="ScreenStack"/>.</summary>
public interface IScreen
{
    /// <summary>When true, screens below this one are not updated (they are paused).</summary>
    bool BlocksUpdateBelow { get; }

    /// <summary>When true, screens below this one are not drawn (this one is opaque).</summary>
    bool BlocksDrawBelow { get; }

    /// <summary>Called when the screen is pushed onto the stack.</summary>
    void Enter();

    /// <summary>Called when the screen leaves the stack (popped, replaced or the game exits).</summary>
    void Exit();

    /// <summary>Updates the screen. Handle input through <paramref name="input"/> and consume what is used.</summary>
    void Update(InputFrame input, GameTime t);

    /// <summary>Draws the screen; the sprite batch is already begun.</summary>
    void Draw(SpriteBatch sb);
}
