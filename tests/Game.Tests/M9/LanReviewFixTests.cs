// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan;
using Rts.Sim.Core;

namespace Rts.Game.Tests.M9;

/// <summary>M9-14 review findings 2, 7 and 8 on whole LAN matches (loopback network, real sims, virtual clock).</summary>
public sealed class LanReviewFixTests
{
    private static List<string> Lines(LanSession s)
    {
        var lines = new List<string>();
        LanOverlay.Describe(s, lines, new List<(string, LanOverlayAction)>());
        return lines;
    }

    private static List<string> Buttons(LanSession s)
    {
        var buttons = new List<(string Label, LanOverlayAction Action)>();
        LanOverlay.Describe(s, new List<string>(), buttons);
        return buttons.Select(b => b.Label).ToList();
    }

    [Fact]
    public void After_the_host_picks_continue_the_clients_leave_the_desync_state()
    {
        using var h = new LanSessionHarness(seed: 13);
        h.RunToTick(200);
        h.Host.Host!.EndTick = null;
        h.Sessions[1].Source.Sim.Players.Cash[1] += 555;
        h.RunUntil(() => h.Host.DesyncTick is not null && h.Sessions.Skip(1).All(s => s.DesyncTick is not null));
        h.Sessions[1].Source.Sim.Players.Cash[1] -= 555;   // back in step, so no new mismatch follows
        for (int i = 0; i < 200; i++) h.Advance();   // hashes taken before the fix are in and absorbed by the open desync

        h.Host.ContinueAfterDesync();
        h.RunUntil(() => h.Sessions.All(s => !s.Paused));
        foreach (LanSession s in h.Sessions)
        {
            Assert.Null(s.DesyncTick);
            Assert.DoesNotContain(LanText.DesyncClient, Lines(s));
        }

        // a client can pause and resume again afterwards
        h.Sessions[2].RequestPause();
        h.RunUntil(() => h.Sessions.All(s => s.Paused));
        h.Sessions[2].RequestResume();
        h.RunUntil(() => h.Sessions.All(s => !s.Paused));
    }

    [Fact]
    public void A_player_who_left_between_start_and_the_match_is_dropped_and_taken_over()
    {
        using var h = new LanSessionHarness(seed: 17, goneBeforeStart: 2);
        h.RunToTick(400, 0, 1, 3);
        Assert.True(h.Host.Host!.IsDropped(2));
        Assert.Contains(h.Host.Chat, l => l.System && l.Text == "Jun left; an AI takes over");
        Assert.Contains(h.Stepped[0], e => e.Command.Player == 2);   // the takeover AI plays the slot
        Assert.Contains(h.Stepped[1], e => e.Command.Player == 2);
        Assert.Equal(h.HashOf(0), h.HashOf(1));
    }

    [Fact]
    public void A_failed_resync_save_tells_the_host_and_leaves_continue_and_end()
    {
        using var h = new LanSessionHarness(seed: 19);
        h.RunToTick(200);
        h.Host.Host!.EndTick = null;
        h.Sessions[1].Source.Sim.Players.Cash[1] += 321;
        h.RunUntil(() => h.Host.DesyncTick is not null);

        h.Host.ResyncSaveOverride = () => throw new IOException("disk full");
        h.Host.ResyncFromHost();
        for (int i = 0; i < 300; i++) h.Advance();

        Assert.False(h.Host.Resyncing);
        Assert.NotNull(h.Host.DesyncTick);
        Assert.Contains(LanText.Continue, Buttons(h.Host));
        Assert.Contains(LanText.EndMatch, Buttons(h.Host));
        Assert.Contains(Lines(h.Host), l => l.Contains("disk full", StringComparison.Ordinal));
        Assert.Equal(0, h.Sessions[1].ResyncCount);

        h.Sessions[1].Source.Sim.Players.Cash[1] -= 321;
        for (int i = 0; i < 200; i++) h.Advance();
        h.Host.ResyncSaveOverride = null;
        h.Host.ContinueAfterDesync();
        h.RunUntil(() => h.Sessions.All(s => !s.Paused && s.DesyncTick is null));
    }
}
