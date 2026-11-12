// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan.Lobby;
using Rts.Net;
using Xunit;

namespace Rts.Game.Tests.M9;

/// <summary>M9-13: the host checks every Hello and either welcomes the joiner or rejects with an exact reason.</summary>
public sealed class LobbyHandshakeTests
{
    [Fact]
    public void MatchingClientIsWelcomedIntoTheFirstOpenSlot()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        Assert.Equal(LobbyClientState.InLobby, ann.State);
        Assert.Equal(1, ann.LocalPeer);
        Assert.Equal(1, ann.LocalSlot);
        Assert.Equal("Ann", rig.Host.Data.Slots[1].Name);
        Assert.Equal(ann.Data, rig.Host.Data);
        Assert.Contains("Ann joined.", rig.Host.Lines);
    }

    [Fact]
    public void ProtocolMismatchIsRejectedWithBothVersions()
    {
        using var rig = new LobbyRig();
        LobbyClient c = rig.Join("Ann", protocol: 99);
        rig.Pump();
        Assert.Equal(LobbyClientState.Rejected, c.State);
        Assert.Contains("99", c.Reason);
        Assert.Contains(NetProtocol.Version.ToString(), c.Reason);
        Assert.Equal(0, rig.Host.Data.People - 1);
    }

    [Fact]
    public void BuildMismatchIsRejectedWithBothVersions()
    {
        using var rig = new LobbyRig();
        LobbyClient c = rig.Join("Ann", version: "0.9-old");
        rig.Pump();
        Assert.Equal(LobbyClientState.Rejected, c.State);
        Assert.Contains("0.9-old", c.Reason);
        Assert.Contains(LobbyRig.Version, c.Reason);
    }

    [Fact]
    public void ContentMismatchIsRejectedWithBothHashes()
    {
        using var rig = new LobbyRig();
        LobbyClient c = rig.Join("Ann", hash: 0xDEAD);
        rig.Pump();
        Assert.Equal(LobbyClientState.Rejected, c.State);
        Assert.Contains($"{0xDEADUL:x16}", c.Reason);
        Assert.Contains($"{LobbyRig.Hash:x16}", c.Reason);
    }

    [Fact]
    public void FullLobbyRejectsPlayersButTakesSpectators()
    {
        using var rig = new LobbyRig();
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        LobbyClient late = rig.Join("Late");
        LobbyClient watcher = rig.Join("Watcher", spectator: true);
        rig.Pump();
        Assert.Equal(LobbyClientState.InLobby, ann.State);
        Assert.Equal(LobbyClientState.Rejected, late.State);
        Assert.Equal("The lobby is full.", late.Reason);
        Assert.Equal(LobbyClientState.InLobby, watcher.State);
        Assert.Equal(-1, watcher.LocalSlot);
        Assert.Single(rig.Host.Data.Spectators);
    }

    [Fact]
    public void ThirdSpectatorIsRejected()
    {
        using var rig = new LobbyRig();
        var w = new[] { rig.Join("A", spectator: true), rig.Join("B", spectator: true), rig.Join("C", spectator: true) };
        rig.Pump();
        Assert.Equal(LobbyClientState.InLobby, w[0].State);
        Assert.Equal(LobbyClientState.InLobby, w[1].State);
        Assert.Equal(LobbyClientState.Rejected, w[2].State);
        Assert.Contains("spectator", w[2].Reason);
    }

    [Fact]
    public void AfterStartNobodyCanJoin()
    {
        using var rig = new LobbyRig();
        rig.Join("Ann");
        rig.Pump();
        rig.Host.SetSlotKind(2, Rts.Game.Screens.SlotKind.Human);
        rig.Clients[0].SetReady(true);
        rig.Pump();
        Assert.NotNull(rig.Host.Start(out _));
        LobbyClient late = rig.Join("Late");
        rig.Pump();
        Assert.Equal(LobbyClientState.Rejected, late.State);
        Assert.Equal("The game has already started.", late.Reason);
    }

    [Fact]
    public void DuplicateNamesGetANumber()
    {
        using var rig = new LobbyRig();
        rig.Host.SetSlotKind(2, Rts.Game.Screens.SlotKind.Human);
        LobbyClient a = rig.Join("Hosty");
        rig.Pump();
        Assert.Equal("Hosty 2", rig.Host.Data.Slots[a.LocalSlot].Name);
    }

    [Fact]
    public void HandshakeOrderChecksProtocolBeforeVersionBeforeData()
    {
        using var rig = new LobbyRig();
        var hello = new NetMessage.Hello(99, "x", 1, "A", false);
        Assert.Contains("protocol", rig.Host.Handshake(hello)!);
        Assert.Contains("version", rig.Host.Handshake(hello with { Protocol = NetProtocol.Version })!);
        Assert.Contains("data", rig.Host.Handshake(hello with { Protocol = NetProtocol.Version, BuildVersion = LobbyRig.Version })!);
        Assert.Null(rig.Host.Handshake(hello with { Protocol = NetProtocol.Version, BuildVersion = LobbyRig.Version, ContentHash = LobbyRig.Hash }));
    }
}
