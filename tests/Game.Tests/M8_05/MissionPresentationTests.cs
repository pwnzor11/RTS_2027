// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Scripting;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M8_05;

/// <summary>M8-05 HUD model: dialogue lines one at a time, the camera hint honoured once, and the voice file only when it exists.</summary>
public sealed class MissionPresentationTests
{
    private static MissionDialogue Line(int seq, string text = "x") => new(seq, 0, "strand", "strand_stern", "k" + seq, text, "");

    [Fact]
    public void Lines_show_one_at_a_time_in_order_for_their_show_time()
    {
        var queue = new List<MissionDialogue> { Line(1, "first"), Line(2, "second") };
        var p = new MissionDialoguePresenter();

        p.Advance(queue, 100);
        Assert.Equal("first", p.Current!.Text);
        Assert.True(p.Started);

        p.Advance(queue, 100 + MissionDialoguePresenter.ShowTicks - 1);
        Assert.Equal("first", p.Current!.Text);
        Assert.False(p.Started);

        p.Advance(queue, 100 + MissionDialoguePresenter.ShowTicks);
        Assert.Equal("second", p.Current!.Text);
        Assert.True(p.Started);

        p.Advance(queue, 100 + 2 * MissionDialoguePresenter.ShowTicks);
        Assert.Null(p.Current);
    }

    [Fact]
    public void A_shown_line_is_not_shown_again_and_a_later_line_starts_after_it()
    {
        var queue = new List<MissionDialogue> { Line(1) };
        var p = new MissionDialoguePresenter();
        p.Advance(queue, 0);
        p.Advance(queue, MissionDialoguePresenter.ShowTicks);
        Assert.Null(p.Current);

        queue.Add(Line(2, "later"));
        p.Advance(queue, MissionDialoguePresenter.ShowTicks + 1);
        Assert.Equal("later", p.Current!.Text);
    }

    [Fact]
    public void Clearing_the_queue_hides_the_line_on_screen()
    {
        var queue = new List<MissionDialogue> { Line(1), Line(2) };
        var p = new MissionDialoguePresenter();
        p.Advance(queue, 0);
        queue.Clear();
        p.Advance(queue, 1);
        Assert.Null(p.Current);

        queue.Add(Line(3, "after clear"));
        p.Advance(queue, 2);
        Assert.Equal("after clear", p.Current!.Text);
    }

    [Fact]
    public void A_line_dropped_from_a_full_queue_is_hidden()
    {
        var queue = new List<MissionDialogue> { Line(1) };
        var p = new MissionDialoguePresenter();
        p.Advance(queue, 0);
        queue.Clear();
        queue.Add(Line(2));
        p.Advance(queue, 1);
        Assert.Equal(2, p.Current!.Seq);
    }

    [Fact]
    public void Voice_file_is_played_only_when_the_file_exists()
    {
        var withVoice = new MissionDialogue(1, 0, "strand", "p", "m1.warning", "t", "voice/strand/m1.warning.ogg");
        var present = new HashSet<string> { "voice/strand/m1.warning.ogg" };
        Assert.Equal("voice/strand/m1.warning.ogg", MissionDialoguePresenter.VoiceToPlay(withVoice, present.Contains));
        Assert.Null(MissionDialoguePresenter.VoiceToPlay(withVoice, _ => false));

        var textOnly = withVoice with { Voice = "" };
        Assert.Null(MissionDialoguePresenter.VoiceToPlay(textOnly, present.Contains));
    }

    [Fact]
    public void A_new_camera_hint_is_honoured_once()
    {
        var cue = new MissionCameraCue();
        var hint = new MissionCameraHint(1, "z", new CellCoord(12, 12), 500);
        Assert.Equal(new CellCoord(12, 12), cue.Take(hint, 100));
        Assert.Null(cue.Take(hint, 101));
    }

    [Fact]
    public void Without_a_hint_or_after_the_window_the_camera_does_not_move()
    {
        var cue = new MissionCameraCue();
        Assert.Null(cue.Take(default, 0));
        Assert.Null(cue.Take(new MissionCameraHint(2, "z", new CellCoord(12, 12), 50), 50));
    }

    [Fact]
    public void Only_the_latest_of_several_hints_is_panned_to()
    {
        var cue = new MissionCameraCue();
        var first = new MissionCameraHint(1, "z", new CellCoord(12, 12), 500);
        var latest = new MissionCameraHint(2, "t1", new CellCoord(30, 30), 500);
        Assert.Equal(new CellCoord(30, 30), cue.Take(latest, 10));
        Assert.Null(cue.Take(first, 10));
    }

    [Fact]
    public void A_hint_whose_target_is_gone_is_skipped()
    {
        var cue = new MissionCameraCue();
        Assert.Null(cue.Take(new MissionCameraHint(1, "t1", new CellCoord(-1, -1), 500), 10));
    }
}
