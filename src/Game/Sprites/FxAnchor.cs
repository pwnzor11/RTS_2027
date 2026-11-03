// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Sprites;

/// <summary>
/// One <c>[[fx]]</c> table of a building manifest: an effect animation drawn at a pixel of the frame while the
/// building shows <see cref="State"/>.
/// </summary>
/// <param name="State">Building state the effect belongs to, for example "damaged".</param>
/// <param name="Kind">Effect sprite id, for example "fire_large". The effect's own origin lands on the anchor.</param>
/// <param name="X">Anchor X in frame space (pixels from the frame's left edge).</param>
/// <param name="Y">Anchor Y in frame space (pixels from the frame's top edge).</param>
/// <param name="Phase">Frame offset, so neighbouring fires do not flicker in sync.</param>
public sealed record FxAnchor(string State, string Kind, int X, int Y, int Phase);
