// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Data;

/// <summary>One finding of <see cref="MapValidator.Validate"/> (M4). Code is one of starts_count, start_space, start_distance,
/// connectivity_foot, connectivity_tracked, feature_cell, few_fields, symmetry; Cell is where to centre the camera, if anywhere.</summary>
public sealed record MapIssue(MapIssueSeverity Severity, string Code, CellCoord? Cell, string Message);
