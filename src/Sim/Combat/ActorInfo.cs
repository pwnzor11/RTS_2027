// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Combat;

/// <summary>Combat-relevant facts about a live actor. Position = unit position, or footprint centre for buildings.</summary>
public readonly record struct ActorInfo(ActorKind Kind, byte Owner, LeptonCoord Position, ArmorClass Armor, int Health);
