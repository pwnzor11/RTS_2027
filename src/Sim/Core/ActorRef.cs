// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>An <see cref="EntityId"/> together with the store it indexes into.</summary>
public readonly record struct ActorRef(ActorKind Kind, EntityId Id);
