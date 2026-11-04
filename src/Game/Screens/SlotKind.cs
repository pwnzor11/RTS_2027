// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>Who plays a skirmish slot (design 08 §2, M4): nobody, the local human, or an AI. Append only.</summary>
public enum SlotKind : byte { Closed = 0, Human = 1, Ai = 2 }
