// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Render;

/// <summary>
/// A prop atlas that draws only some <see cref="PropKind"/>s (a data-backed source with gaps). <see cref="LayeredPropAtlas"/> asks it which kinds it
/// has and takes the others from the next source in its priority order. An <see cref="IPropAtlas"/> that is not an <see cref="IPropSource"/> covers every kind.
/// </summary>
public interface IPropSource : IPropAtlas
{
    /// <summary>True when this source has art for <paramref name="kind"/> (at least one variant).</summary>
    bool Covers(PropKind kind);
}
