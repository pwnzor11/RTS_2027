// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>One scripted radio or dialogue line for the game layer (<c>Dialogue.Say</c>, M8-05). An output: not saved, not hashed.</summary>
/// <param name="Seq">Ever-increasing number; the UI remembers the last one it showed.</param>
/// <param name="Tick">Sim tick the line was queued.</param>
/// <param name="Speaker">Speaker id (a leader or unit id, never display text).</param>
/// <param name="Portrait">Portrait id.</param>
/// <param name="Key">Locale key of the text.</param>
/// <param name="Voice">Optional voice file ("voice/&lt;speaker&gt;/&lt;key&gt;.ogg", D-154), or empty.</param>
public sealed record MissionDialogue(int Seq, int Tick, string Speaker, string Portrait, string Key, string Voice);
