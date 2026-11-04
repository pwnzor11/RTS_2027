// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;

namespace Rts.AI;

/// <summary>What an AI is told when it starts: who it is, its profile, its own seeded generator and where its
/// forces began (the centre of its starting actors). Map size is included so it can mirror positions.</summary>
public sealed class AiContext
{
    /// <summary>The slot this AI plays.</summary>
    public required byte Player { get; init; }

    /// <summary>Number of player slots.</summary>
    public required int PlayerCount { get; init; }

    /// <summary>The game seed (for reference; use <see cref="Rng"/> for randomness).</summary>
    public required ulong GameSeed { get; init; }

    /// <summary>Capability profile for this slot.</summary>
    public required DifficultyProfile Profile { get; init; }

    /// <summary>Seeded from (game seed, slot): reproducible.</summary>
    public required AiRandom Rng { get; init; }

    /// <summary>Map width in cells.</summary>
    public required int MapWidth { get; init; }

    /// <summary>Map height in cells.</summary>
    public required int MapHeight { get; init; }

    /// <summary>Centre of the player's own actors when the AI started.</summary>
    public required CellCoord StartCell { get; init; }

    /// <summary>M4-12: every start position of the map (public map data, like the map size), in map order. Empty when the host does
    /// not supply them; the AI then falls back to mirroring its own start.</summary>
    public IReadOnlyList<CellCoord> StartPositions { get; init; } = Array.Empty<CellCoord>();
}
