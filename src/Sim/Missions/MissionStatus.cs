// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Missions;

/// <summary>Per-mission sub-state, reset to Init when a mission commences. Persisted; append only.</summary>
public enum MissionStatus : byte { Init = 0, Turning = 1, Driving = 2, Waiting = 3 }
