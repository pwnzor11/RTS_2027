// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan;
using Rts.Game.Lan.Lobby;
using Rts.Game.Screens;
using Rts.Net;
using Xunit;

namespace Rts.Game.Tests.M9;

/// <summary>M9-06 (Q-M9-k): a custom map the guest lacks is sent as FileChunk kind 0, stored under maps/downloaded and checked by hash.</summary>
public sealed class LobbyMapTransferTests
{
    private static byte[] MapBytes(int size)
    {
        var b = new byte[size];
        for (int i = 0; i < size; i++) b[i] = (byte)('a' + i * 7 % 23);
        return b;
    }

    [Fact]
    public void ChunkerAndReceiverRoundTrip()
    {
        byte[] data = MapBytes(5321);
        IReadOnlyList<NetMessage.FileChunk> chunks = MapChunker.Split(data, 3);
        Assert.Equal(6, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Data.Length <= NetCodec.MaxChunkBytes));
        var r = new MapReceiver();
        byte[]? got = null;
        foreach (NetMessage.FileChunk c in chunks.Reverse()) got = r.Accept(c) ?? got;   // any order
        Assert.Equal(data, got);
    }

    [Fact]
    public void ReceiverRefusesBadChunks()
    {
        var r = new MapReceiver();
        Assert.Throws<InvalidDataException>(() => r.Accept(new NetMessage.FileChunk(0, 1, 5, 3, new byte[10])));
        Assert.Throws<InvalidDataException>(() => r.Accept(new NetMessage.FileChunk(0, 1, 0, 0, new byte[10])));
        Assert.Throws<InvalidDataException>(() => r.Accept(new NetMessage.FileChunk(0, 1, 0, 5_000_000, new byte[10])));
        Assert.Null(r.Accept(new NetMessage.FileChunk(1, 1, 0, 1, new byte[10])));   // a resync save is not ours
    }

    [Fact]
    public void OversizeMapsAreNotChunked() =>
        Assert.Empty(MapChunker.Split(new byte[MapReceiver.MaxBytes + 1], 1));

    [Fact]
    public void GuestWithoutTheMapDownloadsItAndCanThenReady()
    {
        using var rig = new LobbyRig();
        byte[] bytes = MapBytes(4200);
        File.WriteAllBytes(Path.Combine(rig.UserDir, "arena.map.toml"), bytes);
        var map = new LobbyMap("arena.map.toml", ContentHash.OfBytes(bytes), null);
        Assert.True(rig.Host.SetMap(map));

        LobbyClient ann = rig.Join("Ann");
        rig.Pump();

        Assert.True(ann.HasMap);
        string stored = map.DownloadPath(rig.GuestDownDir(0));
        Assert.True(File.Exists(stored));
        Assert.Equal(bytes, File.ReadAllBytes(stored));
        ann.SetReady(true);
        rig.Pump();
        Assert.True(rig.Host.Data.Slots[1].Ready);

        LanMatchSetup? host = rig.Host.Start(out string? why);
        Assert.True(host is not null, why);
        rig.Pump();
        Assert.Equal(LobbyClientState.Started, ann.State);
        Assert.Equal(stored, ann.Setup!.Settings.Map.File);
        Assert.Equal(Path.Combine(rig.UserDir, "arena.map.toml"), host!.Settings.Map.File);
    }

    [Fact]
    public void GuestWhoAlreadyHasTheMapDoesNotNeedTheDownload()
    {
        using var rig = new LobbyRig();
        byte[] bytes = MapBytes(1500);
        File.WriteAllBytes(Path.Combine(rig.UserDir, "arena.map.toml"), bytes);
        Directory.CreateDirectory(rig.NextGuestUserDir);
        File.WriteAllBytes(Path.Combine(rig.NextGuestUserDir, "arena.map.toml"), bytes);
        var map = new LobbyMap("arena.map.toml", ContentHash.OfBytes(bytes), null);
        rig.Host.SetMap(map);
        LobbyClient ann = rig.Join("Ann");
        rig.Pump();
        Assert.True(ann.HasMap);
        Assert.False(File.Exists(map.DownloadPath(rig.GuestDownDir(0))));
    }

    [Fact]
    public void DownloadWithTheWrongHashIsNotStored()
    {
        using var rig = new LobbyRig();
        byte[] bytes = MapBytes(1500);
        File.WriteAllBytes(Path.Combine(rig.UserDir, "arena.map.toml"), bytes);
        var map = new LobbyMap("arena.map.toml", ContentHash.OfBytes(bytes), null);
        rig.Host.SetMap(map);
        File.WriteAllBytes(Path.Combine(rig.UserDir, "arena.map.toml"), MapBytes(1400));   // the host's file changes: it sends nothing itself now
        LobbyClient ann = rig.Join("Ann");
        rig.Pump(4);
        // a corrupt copy arrives
        foreach (NetMessage.FileChunk c in MapChunker.Split(MapBytes(1400), 99)) rig.Net.Host.Send(ann.LocalPeer, c);
        rig.Pump();
        Assert.Contains(ann.Lines, l => l.Contains("does not match"));
        Assert.False(ann.HasMap);
        Assert.False(File.Exists(map.DownloadPath(rig.GuestDownDir(0))));
        ann.SetReady(true);   // refused while the map is missing
        rig.Pump();
        Assert.False(rig.Host.Data.Slots[1].Ready);
    }

    [Fact]
    public void HostCannotChooseAMapItDoesNotHave()
    {
        using var rig = new LobbyRig();
        Assert.False(rig.Host.SetMap(new LobbyMap("ghost.map.toml", 12345UL, null)));
        Assert.Equal(LobbyMap.Curated, rig.Host.Data.Map);
    }
}
