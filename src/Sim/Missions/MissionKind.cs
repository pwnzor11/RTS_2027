// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Missions;

/// <summary>Actor mission. Values are persisted in saves and replays; append only.
/// M3 (design 05): EnterTunnel = walk to an entrance and queue (M3-02); Transit = inside the network (M3-02); Burrow = dive and
/// travel underground (M3-03); Surface = surfacing telegraph (M3-03); Deploy = go to the deploy cell and deploy (M3-03);
/// Capture = engineer walking to its target building (M3-06). Until filled they run the idle handler.</summary>
public enum MissionKind : byte { None = 0, Sleep = 1, Guard = 2, Move = 3, Attack = 4, AttackMove = 5, Stop = 6,
    Harvest = 7, Return = 8, EnterTunnel = 9, Transit = 10, Burrow = 11, Surface = 12, Deploy = 13, Capture = 14 }
