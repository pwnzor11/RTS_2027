// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Rules;

/// <summary>Classification of <see cref="OverlayKind"/> values (M12-04, D-180). Append-only enum, so a new prop kind only has to be
/// added after <see cref="OverlayKind.Snag"/> and counted here.</summary>
public static class OverlayKinds
{
    /// <summary>The last defined overlay value (the loader's range check).</summary>
    public const OverlayKind Last = OverlayKind.Snag;

    /// <summary>True for the blocking props (Tree, Wreck, Block, Pylon, Snag): not resources. Tree, Snag and Wreck can be destroyed
    /// (<see cref="IsDestructible"/>), Block and Pylon never.</summary>
    public static bool IsProp(OverlayKind k) => k >= OverlayKind.Tree && k <= Last;

    /// <summary>True for a resource field overlay (Ashfall or Pitchblende).</summary>
    public static bool IsResource(OverlayKind k) => k is OverlayKind.Ashfall or OverlayKind.Pitchblende;

    /// <summary>True when ground units cannot enter the cell: a player-built wall or a blocking prop. Air units ignore both.</summary>
    public static bool BlocksGround(OverlayKind k) => k == OverlayKind.Wall || IsProp(k);

    /// <summary>M12-16 (D-264): true for the props a splash hit can destroy (Tree, Snag, Wreck); they carry health in the "props" part.</summary>
    public static bool IsDestructible(OverlayKind k) => k is OverlayKind.Tree or OverlayKind.Snag or OverlayKind.Wreck;

    /// <summary>M12-16 (D-264): true for the props a heavy ground vehicle crushes by driving into the cell (Tree, Snag).</summary>
    public static bool IsCrushable(OverlayKind k) => k is OverlayKind.Tree or OverlayKind.Snag;
}
