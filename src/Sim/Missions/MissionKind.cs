// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Missions;

/// <summary>Actor mission. Values are persisted in saves and replays; append only.</summary>
public enum MissionKind : byte { None = 0, Sleep = 1, Guard = 2, Move = 3, Attack = 4, AttackMove = 5, Stop = 6,
    Harvest = 7, Return = 8 }
