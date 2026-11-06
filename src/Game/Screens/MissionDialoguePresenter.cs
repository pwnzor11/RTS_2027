// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Scripting;

namespace Rts.Game.Screens;

/// <summary>
/// Shows the mission's dialogue queue (M8-05) one line at a time: each line for <see cref="ShowTicks"/> sim ticks, then the next line that
/// has not been shown yet. Pure model (no drawing, no audio, no clock): it reads <see cref="MissionHost.Dialogue"/> after each tick and
/// never changes the queue. The game keeps running while a line is up (the script does not wait for it).
/// </summary>
public sealed class MissionDialoguePresenter
{
    /// <summary>How long a line stays on screen, in sim ticks (6 seconds at 20 ticks per second).</summary>
    public const int ShowTicks = 120;

    private int _nextSeq;
    private int _until;

    /// <summary>The line on screen now, or null.</summary>
    public MissionDialogue? Current { get; private set; }

    /// <summary>True when the last <see cref="Advance"/> started a new line (the moment a voice file would start).</summary>
    public bool Started { get; private set; }

    /// <summary>
    /// Moves the presenter on to tick <paramref name="tick"/>. The current line ends when its time is up or when the queue no longer holds it
    /// (the script cleared the queue, or the queue overflowed). Then the oldest unshown line in the queue starts, if there is one.
    /// </summary>
    public void Advance(IReadOnlyList<MissionDialogue> queue, int tick)
    {
        Started = false;
        if (Current is { } shown && (tick >= _until || !Holds(queue, shown.Seq))) Current = null;
        if (Current is not null) return;
        foreach (MissionDialogue line in queue)
        {
            if (line.Seq < _nextSeq) continue;
            Current = line;
            _until = tick + ShowTicks;
            _nextSeq = line.Seq + 1;
            Started = true;
            return;
        }
    }

    /// <summary>
    /// The voice file to play for <paramref name="line"/>: its voice path when the line has one and <paramref name="exists"/> says the file is
    /// there; null otherwise, so the line shows as text only.
    /// </summary>
    public static string? VoiceToPlay(MissionDialogue line, Func<string, bool> exists) =>
        !string.IsNullOrEmpty(line.Voice) && exists(line.Voice) ? line.Voice : null;

    private static bool Holds(IReadOnlyList<MissionDialogue> queue, int seq)
    {
        foreach (MissionDialogue line in queue) if (line.Seq == seq) return true;
        return false;
    }
}
