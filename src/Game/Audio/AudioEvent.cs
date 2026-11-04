// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Audio;

/// <summary>One thing that should make a sound. <see cref="Pos"/> is where it happened, or null for a sound with no place (interface sounds, alerts).</summary>
public readonly record struct AudioEvent(AudioEventKind Kind, LeptonCoord? Pos = null);
