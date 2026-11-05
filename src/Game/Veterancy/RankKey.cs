// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Veterancy;

/// <summary>Identifies a vehicle or soldier across snapshots: its kind plus its generational entity id (a reused slot is a new key).</summary>
public readonly record struct RankKey(UnitKind Kind, EntityId Id);
