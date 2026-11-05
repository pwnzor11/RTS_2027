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
    /// <summary>Supply cap (SUP-1, D-125): used + queued supply + this unit would exceed the cap. Units only.</summary>
    SupplyCap = 6,
}

/// <summary>A player's supply (SUP-1): units' supply in use, the cap from buildings (at most <see cref="Ceiling"/>) and the hard ceiling.
/// All three are 0 while the supply rule is switched off.</summary>
public readonly record struct SupplyView(int Used, int Cap, int Ceiling);

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

    /// <summary>M3: the player's faction.</summary>
    public Faction Faction { get; init; }

    /// <summary>M3: researched-upgrade mask (bit i = SimRules.Upgrades[i]).</summary>
    public ulong Upgrades { get; init; }

    /// <summary>M3: upgrade indices a Build(ProductionCategory.Upgrade, index) would be accepted for now (never null).</summary>
    public IReadOnlyList<int> BuildableUpgrades { get; init; } = Array.Empty<int>();

    /// <summary>SUP-1: used / cap / ceiling supply; all zero while the supply rule is off.</summary>
    public SupplyView Supply { get; init; }

    /// <summary>M3-13: the player's own superweapon buildings with their charge, in building slot order (never null).</summary>
    public IReadOnlyList<SuperweaponView> Superweapons { get; init; } = Array.Empty<SuperweaponView>();
}
