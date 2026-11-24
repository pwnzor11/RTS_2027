// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Controls;

/// <summary>One map ping: where, who sent it and when it started (milliseconds on the screen's clock).</summary>
public readonly record struct MapPing(CellCoord Cell, int Player, long StartMs);

/// <summary>
/// A43-QOL-09: the pings on the map and the minimap. One ping per sender at a time (a new one replaces that sender's old one), each
/// lasting <see cref="LifeMs"/>. Graphics-free and clocked by the caller; it never touches the sim.
/// </summary>
public sealed class PingBoard
{
    /// <summary>How long a ping stays, in milliseconds (the C&amp;C Remastered beacon lasts 30 s).</summary>
    public const long LifeMs = 30_000;

    private readonly List<MapPing> _pings = new();

    /// <summary>Adds a ping, replacing the sender's earlier one.</summary>
    public void Add(CellCoord cell, int player, long nowMs)
    {
        _pings.RemoveAll(p => p.Player == player);
        _pings.Add(new MapPing(cell, player, nowMs));
    }

    /// <summary>Drops the pings that have run out.</summary>
    public void Prune(long nowMs) => _pings.RemoveAll(p => nowMs - p.StartMs >= LifeMs);

    /// <summary>The live pings, oldest first.</summary>
    public IReadOnlyList<MapPing> Pings => _pings;

    /// <summary>Age of a ping in 0..1 of its life (for fading).</summary>
    public static float Age(MapPing ping, long nowMs) => Math.Clamp((nowMs - ping.StartMs) / (float)LifeMs, 0f, 1f);
}
