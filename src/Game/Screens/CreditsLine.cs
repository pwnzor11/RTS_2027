// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>One line of the credits roll (M10-20).</summary>
/// <param name="Kind">How it is drawn.</param>
/// <param name="Text">The text (empty for a blank line).</param>
public sealed record CreditsLine(CreditsLineKind Kind, string Text);
