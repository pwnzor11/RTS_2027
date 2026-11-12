// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rts.Game.Input;
using Rts.Game.Screens;

namespace Rts.Game.Lan;

/// <summary>
/// Wraps a menu screen opened over a LAN match (M9-07) so the match below keeps being updated: a LAN game never pauses for a menu
/// (design 20 §4.8), or every other peer would wait. The wrapped screen still takes the input first; the game screen under it then only
/// pumps the network and steps the sim (see GameScreen's LAN mode).
/// </summary>
public sealed class LanCover : IScreen
{
    private readonly IScreen _inner;

    /// <summary>Wraps <paramref name="inner"/>.</summary>
    public LanCover(IScreen inner) => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    /// <summary>The wrapped screen.</summary>
    public IScreen Inner => _inner;

    /// <inheritdoc />
    public bool BlocksUpdateBelow => false;

    /// <inheritdoc />
    public bool BlocksDrawBelow => _inner.BlocksDrawBelow;

    /// <inheritdoc />
    public void Enter() => _inner.Enter();

    /// <inheritdoc />
    public void Exit() => _inner.Exit();

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _inner.Update(input, t);
        input.ConsumeKeyboard();   // nothing typed in the menu reaches the match below
        input.ConsumeMouse();
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb) => _inner.Draw(sb);
}
