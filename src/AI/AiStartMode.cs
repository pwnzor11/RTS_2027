// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI;

/// <summary>How an AI match begins: <see cref="Economy"/> = the shared skirmish start (one Construction Yard and starting
/// cash per player), <see cref="Army"/> = the old fixed start force plus a yard (for the army-only skirmisher tests).</summary>
public enum AiStartMode { Economy = 0, Army = 1 }
