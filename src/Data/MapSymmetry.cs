// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Map symmetry (design 08 §1/§5, M4). TOML <c>[map] symmetry</c>: none, rot2, rot4, mirror_x, mirror_y, mirror_diag.
/// Rot4 and MirrorDiag need a square map. Images of a cell: <see cref="MapSymmetries.Images"/>. Append only.</summary>
public enum MapSymmetry : byte { None = 0, Rot2 = 1, Rot4 = 2, MirrorX = 3, MirrorY = 4, MirrorDiag = 5 }
