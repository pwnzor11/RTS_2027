// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Alerts;

/// <summary>One alert as the deriver reports it: what, when (sim tick) and where (null = no place).</summary>
public readonly record struct GameAlert(AlertKind Kind, int Tick, LeptonCoord? Pos = null);
