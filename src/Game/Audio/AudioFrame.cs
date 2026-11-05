// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Audio;

/// <summary>How the local player's game stands.</summary>
public enum GameResult
{
    /// <summary>Still being played.</summary>
    Running,
    /// <summary>The local player's team won.</summary>
    Won,
    /// <summary>The local player was defeated.</summary>
    Lost,
}

/// <summary>
/// Everything the audio layer reads about one step of the game: two consecutive snapshots, the local player's HUD before and after,
/// the commands still queued, and the projectile positions. Read-only; none of it feeds back into the sim.
/// </summary>
public sealed record AudioFrame(SimSnapshot Previous, SimSnapshot Current, byte LocalPlayer)
{
    /// <summary>The local player's HUD at <see cref="Previous"/> (null = unknown).</summary>
    public PlayerHud? PreviousHud { get; init; }

    /// <summary>The local player's HUD at <see cref="Current"/> (null = unknown).</summary>
    public PlayerHud? CurrentHud { get; init; }

    /// <summary>Commands issued and not yet run (SimHost.Pending), used to hear the player's orders.</summary>
    public IReadOnlyList<Command> Pending { get; init; } = Array.Empty<Command>();

    /// <summary>Projectile positions at <see cref="Previous"/>.</summary>
    public IReadOnlyList<LeptonCoord> PreviousProjectiles { get; init; } = Array.Empty<LeptonCoord>();

    /// <summary>Projectile positions at <see cref="Current"/>.</summary>
    public IReadOnlyList<LeptonCoord> Projectiles { get; init; } = Array.Empty<LeptonCoord>();

    /// <summary>VET-5: vehicles and soldiers whose veterancy rank rose at <see cref="Current"/> (empty when none).</summary>
    public IReadOnlyList<Rts.Game.Veterancy.RankKey> Promotions { get; init; } = Array.Empty<Rts.Game.Veterancy.RankKey>();

    /// <summary>Whether the local player has won or lost.</summary>
    public GameResult Result { get; init; }
}
