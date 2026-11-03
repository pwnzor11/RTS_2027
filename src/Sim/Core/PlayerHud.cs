// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Sim.Core;

/// <summary>One queued item in a production queue. Progress is 0..1000.</summary>
public readonly record struct QueueItemView(int TypeIndex, int ProgressPermille, bool Stalled);

/// <summary>One production queue: its items (front first) and the finished structure waiting to be placed (-1 = none).</summary>
public sealed record QueueView(ProductionCategory Category, IReadOnlyList<QueueItemView> Items, int ReadyTypeIndex);

/// <summary>Everything the HUD needs about one player (M2 contract). Read-only snapshot.</summary>
public sealed record PlayerHud(int Cash, int Stored, int StorageCap, int PowerOut, int PowerUse,
    IReadOnlyList<QueueView> Queues, IReadOnlyList<(ProductionCategory Category, int TypeIndex)> Buildable,
    bool Defeated);
