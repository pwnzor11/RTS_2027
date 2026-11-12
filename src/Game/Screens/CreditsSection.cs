// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>A heading and its entries in the credits roll (M10-20).</summary>
/// <param name="Heading">The section heading.</param>
/// <param name="Entries">The entries, in order.</param>
public sealed record CreditsSection(string Heading, IReadOnlyList<CreditsEntry> Entries);
