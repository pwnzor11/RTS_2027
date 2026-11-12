// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan;
using Rts.Game.Lan.Lobby;
using Rts.Game.Screens;
using Rts.Sim.Rules;
using Xunit;

namespace Rts.Game.Tests.M9;

/// <summary>M9-05/07: lobby state transitions on the host, requests from guests, and the Start hand-off.</summary>
public sealed class LobbyStateTests
{
    [Fact]
    public void GuestRequestChangesOnlyItsOwnSlotAndClearsReady()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        ann.SetReady(true);
        rig.Pump();
        Assert.True(rig.Host.Data.Slots[1].Ready);
        ann.Request(new SlotChange(Faction.Directorate, 5, 2));
        rig.Pump();
        LobbySlotState s = rig.Host.Data.Slots[1];
        Assert.Equal(Faction.Directorate, s.Slot.Faction);
        Assert.Equal(5, s.Slot.Colour);
        Assert.Equal(2, s.Slot.Team);
        Assert.False(s.Ready);
        Assert.Equal(Faction.Authority, rig.Host.Data.Slots[0].Slot.Faction);
    }

    [Fact]
    public void ColourTakenByAnotherSlotIsKeptOff()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        ann.Request(new SlotChange(Faction.Authority, 0, 1));   // colour 0 is the host's
        rig.Pump();
        Assert.Equal(1, rig.Host.Data.Slots[1].Slot.Colour);
    }

    [Fact]
    public void GuestCanMoveToAnOpenSlotOnly()
    {
        using var rig = new LobbyRig();
        rig.Host.SetSlotKind(2, SlotKind.Human);
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        ann.Request(new SlotChange(Faction.Authority, 1, 1, 2));
        rig.Pump();
        Assert.Equal(2, ann.LocalSlot);
        Assert.True(rig.Host.Data.Slots[1].IsOpen);
        ann.Request(new SlotChange(Faction.Authority, 1, 1, 0));   // the host's slot is not open
        rig.Pump();
        Assert.Equal(2, ann.LocalSlot);
    }

    [Fact]
    public void StartNeedsEveryHumanReady()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        Assert.False(rig.Host.CanStart(out string? why));
        Assert.Contains("Ann", why);
        ann.SetReady(true);
        rig.Pump();
        Assert.True(rig.Host.CanStart(out why));
        Assert.Null(why);
    }

    [Fact]
    public void StartRefusesInvalidSettings()
    {
        using var rig = new LobbyRig();
        // only the host is open: one player cannot start
        Assert.False(rig.Host.CanStart(out string? why));
        Assert.NotNull(why);
        rig.Host.SetSlotKind(1, SlotKind.Ai);
        rig.Host.UpdateSlot(1, s => s with { Team = 0 });
        Assert.False(rig.Host.CanStart(out why));
        Assert.Contains("one team", why);
        rig.Host.UpdateSlot(1, s => s with { Team = 1 });
        Assert.True(rig.Host.CanStart(out _));
    }

    [Fact]
    public void LeavingFreesTheSlotAndKickWorks()
    {
        using var rig = new LobbyRig();
        rig.Host.SetSlotKind(2, SlotKind.Human);
        LobbyClient ann = rig.Join("Ann");
        LobbyClient bob = rig.Join("Bob");
        rig.Pump();
        ann.Leave();
        rig.Pump();
        Assert.True(rig.Host.Data.Slots[1].IsOpen);
        rig.Host.Kick(bob.LocalPeer, "Out");
        rig.Pump();
        Assert.Equal(LobbyClientState.Disconnected, bob.State);
        Assert.Equal("Out", bob.Reason);
        Assert.True(rig.Host.Data.Slots[2].IsOpen);
    }

    [Fact]
    public void ClosingASlotWithAPlayerSendsThemAway()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        rig.Host.SetSlotKind(1, SlotKind.Ai);
        rig.Pump();
        Assert.Equal(LobbyClientState.Disconnected, ann.State);
        Assert.Equal(SlotKind.Ai, rig.Host.Data.Slots[1].Slot.Kind);
        Assert.Equal(-1, rig.Host.Data.Slots[1].Peer);
    }

    [Fact]
    public void ChatReachesEveryone()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        ann.Say("hello all");
        rig.Pump();
        Assert.Contains("Ann: hello all", rig.Host.Lines);
        Assert.Contains("Ann: hello all", ann.Lines);
        rig.Host.Say(0, "welcome");
        rig.Pump();
        Assert.Contains("Hosty: welcome", ann.Lines);
    }

    [Fact]
    public void DelayAndMapChangesReachGuests()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        rig.Host.SetDelay(4);
        rig.Host.SetDelay(7);   // not a choice: ignored
        rig.Pump();
        Assert.Equal(4, ann.Data!.CommandDelay);
    }

    [Fact]
    public void StartGivesHostAndGuestsTheSameMatch()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        LobbyClient watcher = rig.Join("Eve", spectator: true);
        rig.Pump();
        rig.Host.SetSlotKind(2, SlotKind.Ai);
        rig.Host.UpdateSlot(2, s => s with { Team = 1, AiTier = 3 });
        ann.SetReady(true);
        rig.Pump();
        LanMatchSetup? host = rig.Host.Start(out string? why);
        Assert.NotNull(host);
        Assert.Null(why);
        rig.Pump();
        Assert.Equal(LobbyClientState.Started, ann.State);
        Assert.Equal(LobbyClientState.Started, watcher.State);
        LanMatchSetup g = ann.Setup!;

        Assert.True(host!.IsHost);
        Assert.False(g.IsHost);
        Assert.Equal(0, host.LocalPeer);
        Assert.Equal(1, g.LocalPeer);
        Assert.Equal(host.Settings.SimSeed, g.Settings.SimSeed);
        Assert.Equal(4242UL, g.Settings.SimSeed);
        Assert.Equal(host.CommandDelay, g.CommandDelay);
        Assert.True(host.Settings.Slots.SequenceEqual(g.Settings.Slots));
        Assert.Equal(host.Settings.Map, g.Settings.Map);
        Assert.Equal(new[] { 0, 1 }, host.Settings.PlayerSlots.Take(2).ToArray());
        Assert.Equal(0, host.PeerSlots[0]);
        Assert.Equal(1, host.PeerSlots[1]);
        Assert.Equal(host.PeerSlots.OrderBy(p => p.Key), g.PeerSlots.OrderBy(p => p.Key));
        Assert.Equal(new[] { watcher.LocalPeer }, g.Spectators);
        Assert.Equal("Ann", g.PeerNames[1]);
        Assert.Equal("Hosty", g.PeerNames[0]);
        Assert.Empty(host.Settings.Validate(4));
        Assert.Equal(3, host.Settings.PlayerCount);
        Assert.Same(ann.Transport, g.Transport);
        Assert.Same(rig.Host.Transport, host.Transport);
    }

    [Fact]
    public void OpenSlotsBecomeClosedAtStart()
    {
        using var rig = new LobbyRig();
        rig.Host.SetSlotKind(1, SlotKind.Ai);
        rig.Host.SetSlotKind(2, SlotKind.Human);   // stays open
        LanMatchSetup? m = rig.Host.Start(out string? why);
        Assert.NotNull(m);
        Assert.Equal(SlotKind.Closed, m!.Settings.Slots[2].Kind);
        Assert.Equal(2, m.Settings.PlayerCount);
    }

    [Fact]
    public void DiscoveryAnnouncementTracksTheLobby()
    {
        using var rig = new LobbyRig();
        var info = rig.Host.Announcement(47270);
        Assert.True(info.Open);
        Assert.False(info.Started);
        Assert.Equal("Hosty", info.LobbyName);
        Assert.Equal(1, info.Players);
        Assert.True(info.Compatible);
        rig.Join("Ann");
        rig.Pump();
        Assert.Equal(2, rig.Host.Announcement(47270).Players);
    }

    [Fact]
    public void GeneratedMapGetsThePlayerCountOfTheOpenSlots()
    {
        using var rig = new LobbyRig(LobbyMap.FromGenerated(new Rts.Data.MapGenParams(Rts.Data.Biome.RustBelt, Rts.Data.MapSize.Small, 2, 77)));
        rig.Host.SetSlotKind(1, SlotKind.Ai);
        rig.Host.SetSlotKind(2, SlotKind.Ai);
        LanMatchSetup? m = rig.Host.Start(out string? why);
        Assert.NotNull(m);
        Assert.Equal(3, m!.Settings.Map.Generated!.Players);
        Assert.Equal(77UL, m.Settings.SimSeed);
    }
}
