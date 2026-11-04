// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Generated map size (design 08 §1, M4): Small 64×64, Medium 96×96, Large 128×128 (<see cref="MapGenerator.CellsFor"/>). Append only.</summary>
public enum MapSize : byte { Small = 0, Medium = 1, Large = 2 }
