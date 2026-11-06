// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting;

/// <summary>
/// One scripted radio or dialogue line for the game layer (<c>Dialogue.Say</c>, M8-05). An output: not saved, not hashed, never read by the
/// sim. The host keeps the queue in order (oldest first) until the script calls <c>Dialogue.Clear</c>.
/// </summary>
/// <param name="Seq">Ever-increasing number (per host); the game layer remembers the last one it started.</param>
/// <param name="Tick">Sim tick the line was queued.</param>
/// <param name="Speaker">Speaker id (a leader or unit id, never display text).</param>
/// <param name="Portrait">Portrait id.</param>
/// <param name="Key">Locale key of the text.</param>
/// <param name="Text">The text resolved from the locale file; "[key]" when the key is missing.</param>
/// <param name="Voice">Voice file ("voice/&lt;speaker&gt;/&lt;key&gt;.ogg", D-154), or empty when the line has none.</param>
public sealed record MissionDialogue(int Seq, int Tick, string Speaker, string Portrait, string Key, string Text, string Voice);
