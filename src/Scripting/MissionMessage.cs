// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>A Media.Message line for the message ticker.</summary>
/// <param name="Seq">Ever-increasing number; the UI remembers the last one it showed.</param>
/// <param name="Tick">Sim tick the message was sent.</param>
/// <param name="Text">The text.</param>
public sealed record MissionMessage(int Seq, int Tick, string Text);
