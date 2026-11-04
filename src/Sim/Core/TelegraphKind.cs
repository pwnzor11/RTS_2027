// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>What a surfacing telegraph announces (design 05 §1, D-075): a burrowed unit coming up, or a new entrance (Hatch)
/// breaking through. Append only: values are persisted.</summary>
public enum TelegraphKind : byte { Unit = 0, Entrance = 1 }
