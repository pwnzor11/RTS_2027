// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Global constants from data/rules/game.toml.</summary>
public sealed record GameRules(int TickRate, int DefaultMapWidth, int DefaultMapHeight);
