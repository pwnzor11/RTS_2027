// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.View;

namespace Rts.AI;

/// <summary>The "idle" AI: never acts. A sparring dummy and the baseline for tests.</summary>
public sealed class IdleAi : IAiPlayer
{
    /// <inheritdoc />
    public string Id => AiRegistry.IdleId;

    /// <inheritdoc />
    public void Start(AiContext ctx) { }

    /// <inheritdoc />
    public void Think(PlayerView view, ICommandSink sink) { }
}
