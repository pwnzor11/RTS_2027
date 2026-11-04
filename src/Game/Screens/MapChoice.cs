// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;

namespace Rts.Game.Screens;

/// <summary>The skirmish map (design 08 §2, M4): a map file (curated, e.g. "rustbelt_m2.map.toml", or a user map path) or generator
/// parameters. Exactly one is set.</summary>
public sealed record MapChoice(string? File, MapGenParams? Generated);
