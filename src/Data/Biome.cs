// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Procedural map biome (design 08 §1, M4). TOML keys: rust_belt, glass_coast, mountains, ash_plains. Append only.</summary>
public enum Biome : byte { RustBelt = 0, GlassCoast = 1, Mountains = 2, AshPlains = 3 }
