// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>One row of the live stats block (for example "Cost" and "800").</summary>
/// <param name="Label">Row label.</param>
/// <param name="Value">Row value.</param>
public sealed record ArchiveStatLine(string Label, string Value);
