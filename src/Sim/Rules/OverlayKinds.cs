// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Classification of <see cref="OverlayKind"/> values (M12-04, D-180). Append-only enum, so a new prop kind only has to be
/// added after <see cref="OverlayKind.Snag"/> and counted here.</summary>
public static class OverlayKinds
{
    /// <summary>The last defined overlay value (the loader's range check).</summary>
    public const OverlayKind Last = OverlayKind.Snag;

    /// <summary>True for the static blocking props (Tree, Wreck, Block, Pylon, Snag): not resources, not destructible.</summary>
    public static bool IsProp(OverlayKind k) => k >= OverlayKind.Tree && k <= Last;

    /// <summary>True for a resource field overlay (Ashfall or Pitchblende).</summary>
    public static bool IsResource(OverlayKind k) => k is OverlayKind.Ashfall or OverlayKind.Pitchblende;

    /// <summary>True when ground units cannot enter the cell: a player-built wall or a blocking prop. Air units ignore both.</summary>
    public static bool BlocksGround(OverlayKind k) => k == OverlayKind.Wall || IsProp(k);
}
