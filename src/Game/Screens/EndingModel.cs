// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Campaign;

namespace Rts.Game.Screens;

/// <summary>One slide as shown: the image id (may have no file) and the caption text.</summary>
/// <param name="Image">Image id.</param>
/// <param name="Caption">Caption text from the faction ending file.</param>
public sealed record EndingFrame(string Image, string Caption);

/// <summary>The ending sequence stepping (plan 3.8, M8-16) with no graphics: slides filtered by the progress file, in order; next and skip.</summary>
public sealed class EndingModel
{
    private readonly List<EndingFrame> _frames;

    /// <summary>Creates the model from an ending file and the faction's progress.</summary>
    public EndingModel(EndingDef def, CampaignProgress progress)
    {
        _frames = def.SlidesFor(progress).Select(s => new EndingFrame(s.Image, def.Text.GetValueOrDefault(s.Key, s.Key))).ToList();
    }

    /// <summary>The slides that will be shown, in order.</summary>
    public IReadOnlyList<EndingFrame> Frames => _frames;

    /// <summary>Index of the current slide.</summary>
    public int Index { get; private set; }

    /// <summary>True once the last slide was passed or the sequence was skipped (the screen then closes).</summary>
    public bool Finished { get; private set; }

    /// <summary>The slide on screen, or null when finished (or there are none).</summary>
    public EndingFrame? Current => Finished || Index >= _frames.Count ? null : _frames[Index];

    /// <summary>Goes to the next slide; after the last one the sequence is finished.</summary>
    public void Next()
    {
        if (Finished) return;
        if (++Index >= _frames.Count) Finished = true;
    }

    /// <summary>Ends the sequence at once.</summary>
    public void Skip() => Finished = true;
}
