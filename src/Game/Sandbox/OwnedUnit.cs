// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Sandbox;

/// <summary>A unit spawned at setup, with its owner, so scripted orders can pick the right player.</summary>
public readonly record struct OwnedUnit(UnitKey Key, byte Player);
