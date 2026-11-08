// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>
/// A theater's decoration tables, read from the <c>[decor]</c> section of its palette file: per-biome decals (M12-01) and the
/// land-linked sprites for Rock clusters, Rough clusters and river banks (M12-02). A palette without the section has <see cref="Empty"/>.
/// </summary>
/// <param name="Decals">Decal rules in file order.</param>
/// <param name="Rock">Sprite for Rock clusters, or null.</param>
/// <param name="Rough">Sprite for Rough clusters, or null.</param>
/// <param name="Bank">Sprite for water cells that touch land, or null.</param>
public sealed record DecorTable(IReadOnlyList<DecalRule> Decals, SpriteRule? Rock, SpriteRule? Rough, SpriteRule? Bank)
{
    /// <summary>No decoration (the classic look).</summary>
    public static readonly DecorTable Empty = new(Array.Empty<DecalRule>(), null, null, null);

    /// <summary>True when there is nothing to draw.</summary>
    public bool IsEmpty => Decals.Count == 0 && Rock is null && Rough is null && Bank is null;
}
