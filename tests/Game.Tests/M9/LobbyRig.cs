// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Lan.Lobby;
using Rts.Net;
using Rts.Net.Loopback;

namespace Rts.Game.Tests.M9;

/// <summary>Test helper: a host lobby and its clients over the in-process loopback network, driven by a virtual clock (no sleeps).</summary>
internal sealed class LobbyRig : IDisposable
{
    public const ulong Hash = 0xABCDEF;
    public const string Version = "test-1";

    public readonly string Root = Path.Combine(Path.GetTempPath(), "rts_lobby_" + Guid.NewGuid().ToString("N"));
    public readonly LoopbackNetwork Net = new(7);
    public readonly LobbyHost Host;
    public readonly List<LobbyClient> Clients = new();
    private long _now;

    public LobbyRig(LobbyMap? map = null)
    {
        Directory.CreateDirectory(UserDir);
        Host = new LobbyHost(Net.Host, "Hosty", Hash, Version, map ?? LobbyMap.Curated, UserDir, DownDir, () => 4242);
    }

    public string UserDir => Path.Combine(Root, "maps");

    public string DownDir => Path.Combine(Root, "maps", "downloaded");

    /// <summary>The player maps folder the next guest to join will use (each guest has folders of its own, apart from the host's).</summary>
    public string NextGuestUserDir => Path.Combine(Root, "guest" + Clients.Count, "maps");

    /// <summary>The download folder of guest <paramref name="index"/>.</summary>
    public string GuestDownDir(int index) => Path.Combine(Root, "guest" + index, "maps", "downloaded");

    public LobbyClient Join(string name, bool spectator = false, ulong? hash = null, string? version = null, int? protocol = null)
    {
        INetTransport t = Net.AddClient();
        NetMessage.Hello hello = LobbyClient.MakeHello(name, spectator, hash ?? Hash, version ?? Version);
        if (protocol is { } p) hello = hello with { Protocol = p };
        var client = new LobbyClient(t, hello, NextGuestUserDir, GuestDownDir(Clients.Count));
        Clients.Add(client);
        return client;
    }

    public void Pump(int rounds = 60)
    {
        for (int i = 0; i < rounds; i++)
        {
            _now += 10_000;
            Host.Poll(_now);
            foreach (LobbyClient c in Clients) c.Poll(_now);
        }
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }
}
