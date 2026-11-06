// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Scripting.Tests;

/// <summary>M8-05: Dialogue.Say / Clear (queue), Camera.Focus (latest hint wins), Media.MessageKey (locale text).</summary>
public sealed class MissionDialogueTests
{
    [Fact]
    public void Say_queues_the_locale_text_with_speaker_portrait_and_voice()
    {
        var (sim, host) = M85Mission.Build(
            "function OnInit() Dialogue.Say('strand', 'strand_stern', 'm1.warning'); Dialogue.Say('strand', 'strand_stern', 'm1.line2', 'voice/strand/custom.ogg') end",
            M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.Equal(2, host.Dialogue.Count);

        MissionDialogue first = host.Dialogue[0];
        Assert.Equal("strand", first.Speaker);
        Assert.Equal("strand_stern", first.Portrait);
        Assert.Equal("m1.warning", first.Key);
        Assert.Equal("Commander, the convoy is late.", first.Text);
        Assert.Equal("voice/strand/m1.warning.ogg", first.Voice);

        MissionDialogue second = host.Dialogue[1];
        Assert.Equal("Hold the junction.", second.Text);
        Assert.Equal("voice/strand/custom.ogg", second.Voice);
        Assert.True(second.Seq > first.Seq);
    }

    [Fact]
    public void Without_a_voice_argument_or_locale_voice_the_line_is_text_only()
    {
        var (sim, host) = M85Mission.Build("function OnInit() Dialogue.Say('strand', 'strand_stern', 'm1.line2') end", M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Equal(string.Empty, host.Dialogue.Single().Voice);
    }

    [Fact]
    public void The_queue_is_first_in_first_out_and_a_missing_key_shows_in_brackets()
    {
        var (sim, host) = M85Mission.Build(
            "function OnInit() Dialogue.Say('a', 'p', 'm1.line2'); Dialogue.Say('b', 'p', 'm1.warning'); Dialogue.Say('c', 'p', 'm1.gone') end",
            M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Equal(new[] { "a", "b", "c" }, host.Dialogue.Select(d => d.Speaker));
        Assert.Equal("[m1.gone]", host.Dialogue[2].Text);
    }

    [Fact]
    public void A_full_queue_drops_the_oldest_line()
    {
        var (sim, host) = M85Mission.Build("function OnInit() for i = 1, 20 do Dialogue.Say('s', 'p', 'm1.line2') end end", M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Equal(MissionHost.MaxDialogueQueue, host.Dialogue.Count);
        Assert.Equal(20 - MissionHost.MaxDialogueQueue + 1, host.Dialogue[0].Seq);
        Assert.Equal(20, host.Dialogue[^1].Seq);
    }

    [Fact]
    public void Clear_empties_the_queue_and_later_lines_start_a_new_queue()
    {
        var (sim, host) = M85Mission.Build(
            "function OnInit() Dialogue.Say('a', 'p', 'm1.line2'); Dialogue.Say('a', 'p', 'm1.line2'); Dialogue.Clear(); Dialogue.Say('b', 'p', 'm1.warning') end",
            M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        MissionDialogue only = host.Dialogue.Single();
        Assert.Equal("b", only.Speaker);
    }

    [Fact]
    public void Camera_focus_on_a_zone_points_at_its_middle_cell_for_the_given_ticks()
    {
        var (sim, host) = M85Mission.Build("function OnInit() Camera.Focus('z', 120) end", M85Mission.Sample());
        int setAt = sim.Tick;
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        MissionCameraHint hint = host.CameraHint;
        Assert.Equal("z", hint.Target);
        Assert.Equal(new Rts.Sim.Core.CellCoord(12, 12), hint.Cell);
        Assert.Equal(setAt + 120, hint.UntilTick);
        Assert.True(hint.Seq > 0);
    }

    [Fact]
    public void Camera_focus_on_an_actor_points_at_the_actor_and_the_latest_hint_wins()
    {
        var (sim, host) = M85Mission.Build(
            "function OnInit() Actor.Create('auth_rifleman', 0, {x=30, y=30}, 't1'); Camera.Focus('z', 10); Camera.Focus('t1', 50) end",
            M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.Equal("t1", host.CameraHint.Target);
        Assert.Equal(new Rts.Sim.Core.CellCoord(30, 30), host.CameraHint.Cell);
    }

    [Fact]
    public void Media_message_key_adds_the_locale_text_to_the_ticker()
    {
        var (sim, host) = M85Mission.Build("function OnInit() Media.MessageKey('m1.line2'); Media.MessageKey('m1.gone') end", M85Mission.Sample());
        TestData.Run(sim, 1);
        Assert.Empty(host.Errors);
        Assert.Equal(new[] { "Hold the junction.", "[m1.gone]" }, host.Messages.Select(m => m.Text));
    }
}
