// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Audio;

/// <summary>
/// One thing that should make a sound. <see cref="Pos"/> is where it happened, or null for a sound with no place (interface sounds, alerts).
/// <see cref="Class"/> and <see cref="Faction"/> (S22) are the optional keys of the sounds.toml lookup: a unit class (infantry, vehicle, ship,
/// air), a weapon class (small_arms, cannon, rocket ...) or a modifier such as <c>large</c> / <c>bomb</c>; and a faction (authority,
/// directorate, left_behind, expedition). See <see cref="SoundBank.Resolve"/> for the order they are tried in.
/// </summary>
public readonly record struct AudioEvent(AudioEventKind Kind, LeptonCoord? Pos = null, string? Class = null, string? Faction = null);
