// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Sim.Core;

/// <summary>One queued item in a production queue. Progress is 0..1000.</summary>
public readonly record struct QueueItemView(int TypeIndex, int ProgressPermille, bool Stalled);

/// <summary>One production queue: its items (front first) and the finished structure waiting to be placed (-1 = none).</summary>
public sealed record QueueView(ProductionCategory Category, IReadOnlyList<QueueItemView> Items, int ReadyTypeIndex);

/// <summary>Why a Build command for an unlocked type would be rejected right now (None = it would be accepted).</summary>
public enum QueueBlockReason : byte
{
    /// <summary>The type can be queued.</summary>
    None = 0,
    /// <summary>The player is defeated and can no longer act.</summary>
    Defeated = 1,
    /// <summary>Not a buildable type in that category (bad index, wrong category, aircraft).</summary>
    NotBuildable = 2,
    /// <summary>A unit category with no alive factory that produces it.</summary>
    NoFactory = 3,
    /// <summary>A finished structure or defence is waiting to be placed (D-066).</summary>
    StructureWaiting = 4,
    /// <summary>The category's queue is at its limit.</summary>
    QueueFull = 5,
}

/// <summary>
/// One sidebar entry (D-066): a type the player has unlocked, with the reason it cannot be queued right now
/// (<see cref="QueueBlockReason.None"/> when it can) and its cost. <see cref="Unlocked"/> is always true in
/// <see cref="PlayerHud.Options"/>.
/// </summary>
public readonly record struct BuildOption(ProductionCategory Category, int TypeIndex, bool Unlocked,
    QueueBlockReason Blocked, int Cost);

/// <summary>
/// Everything the HUD needs about one player (M2 contract). Read-only snapshot. <see cref="Options"/> lists every
/// unlocked (category, type) with its block reason; <see cref="Buildable"/> is the subset with no block.
/// </summary>
public sealed record PlayerHud(int Cash, int Stored, int StorageCap, int PowerOut, int PowerUse,
    IReadOnlyList<QueueView> Queues, IReadOnlyList<(ProductionCategory Category, int TypeIndex)> Buildable,
    bool Defeated, IReadOnlyList<BuildOption>? Options = null)
{
    /// <summary>Every unlocked type, in category then type order (never null).</summary>
    public IReadOnlyList<BuildOption> Options { get; init; } = Options ?? Array.Empty<BuildOption>();
}
