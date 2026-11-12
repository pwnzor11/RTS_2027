// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using Rts.Game.Screens;
using Rts.Net;
using Rts.Net.Udp;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// The client side of a LAN lobby (M9-05, M9-06, M9-13), without any screen: it sends Hello, follows the host's lobby state, downloads a
/// custom map the client lacks, and turns the host's Start into a <see cref="LanMatchSetup"/>. The screen calls <see cref="Poll"/> each frame.
/// </summary>
public sealed class LobbyClient
{
    private const int MaxLines = 60;

    private readonly INetTransport _transport;
    private readonly string _userMapsDir;
    private readonly string _downloadedDir;
    private readonly MapReceiver _receiver = new();
    private readonly List<string> _lines = new();

    /// <summary>Wraps a transport. <paramref name="hello"/> is sent now unless the transport already queued it (the UDP connect does).</summary>
    public LobbyClient(INetTransport transport, NetMessage.Hello? hello, string userMapsDir, string downloadedDir)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _userMapsDir = userMapsDir;
        _downloadedDir = downloadedDir;
        if (hello is not null) _transport.Send(0, hello);
    }

    /// <summary>Builds the Hello a client sends.</summary>
    public static NetMessage.Hello MakeHello(string playerName, bool spectator, ulong contentHash, string buildVersion) =>
        new(NetProtocol.Version, buildVersion, contentHash, LobbyNames.Resolve(playerName), spectator);

    /// <summary>Connects over UDP to a host; the Hello goes out with the first packet.</summary>
    public static LobbyClient Join(IPEndPoint host, NetMessage.Hello hello, string userMapsDir, string downloadedDir) =>
        new(UdpTransport.Connect(host, hello), null, userMapsDir, downloadedDir);

    /// <summary>Where the client is.</summary>
    public LobbyClientState State { get; private set; } = LobbyClientState.Connecting;

    /// <summary>Why the host refused or the connection ended (empty otherwise).</summary>
    public string Reason { get; private set; } = "";

    /// <summary>The lobby as last received (null before the first state).</summary>
    public LobbyData? Data { get; private set; }

    /// <summary>Bumped on every change, so a screen can tell when to redraw its rows.</summary>
    public int Revision { get; private set; }

    /// <summary>Chat lines, oldest first.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>The match to play, once <see cref="State"/> is <see cref="LobbyClientState.Started"/>.</summary>
    public LanMatchSetup? Setup { get; private set; }

    /// <summary>The transport (the screen disposes it when the client leaves before the match).</summary>
    public INetTransport Transport => _transport;

    /// <summary>This client's peer id (-1 until welcomed).</summary>
    public int LocalPeer => _transport.LocalPeer;

    /// <summary>Our slot index in the lobby, or -1 (spectator or not yet known).</summary>
    public int LocalSlot => Data?.SlotOf(LocalPeer) ?? -1;

    /// <summary>True when we can play the lobby's map: a generated or curated one, or a custom file that is here.</summary>
    public bool HasMap => Data is null || Data.Map.Resolve(_userMapsDir, _downloadedDir) is not null;

    /// <summary>Percent of a custom map downloaded so far (0 when not downloading).</summary>
    public int MapPercent => _receiver.Total == 0 ? 0 : _receiver.Received * 100 / _receiver.Total;

    /// <summary>Pumps the transport and handles what arrived.</summary>
    public void Poll(long nowMicroseconds)
    {
        if (State is LobbyClientState.Rejected or LobbyClientState.Disconnected or LobbyClientState.Started) return;
        _transport.Poll(nowMicroseconds);
        while (State is LobbyClientState.Connecting or LobbyClientState.InLobby && _transport.TryReceive(out _, out NetMessage message)) Handle(message);
        while (_transport.TryGetEvent(out PeerEvent e))
        {
            if (e.Connected || e.Peer != 0) continue;
            if (State is LobbyClientState.Connecting or LobbyClientState.InLobby)
                End(LobbyClientState.Disconnected, State == LobbyClientState.Connecting ? "Could not reach the host: " + e.Reason : "Lost the connection to the host: " + e.Reason);
        }
    }

    private void End(LobbyClientState state, string reason)
    {
        State = state;
        Reason = reason;
        Revision++;
    }

    private void Handle(NetMessage message)
    {
        switch (message)
        {
            case NetMessage.Welcome:
                if (State == LobbyClientState.Connecting) { State = LobbyClientState.InLobby; Revision++; }
                break;
            case NetMessage.Reject r: End(LobbyClientState.Rejected, r.Reason); break;
            case NetMessage.Bye b: End(LobbyClientState.Disconnected, string.IsNullOrEmpty(b.Reason) ? "The host closed the lobby." : b.Reason); break;
            case NetMessage.LobbyState s when State == LobbyClientState.InLobby:
                try { Data = LobbyCodec.Decode(s.Blob); Revision++; }
                catch (InvalidDataException) { Leave("The host sent a lobby this version does not understand."); }
                break;
            case NetMessage.Chat c when State == LobbyClientState.InLobby: AddLine(NameOf(c.FromPeer) + ": " + c.Text); break;
            case NetMessage.FileChunk f when State == LobbyClientState.InLobby: TakeChunk(f); break;
            case NetMessage.Ping p: _transport.Send(0, new NetMessage.Pong(p.Stamp), reliable: false); break;
            case NetMessage.Start st when State == LobbyClientState.InLobby: TakeStart(st); break;
        }
    }

    private string NameOf(int peer) => peer == 0 ? Data?.HostName ?? "Host"
        : Data?.Slots.FirstOrDefault(s => s.IsTaken && s.Peer == peer)?.Name ?? Data?.Spectators.FirstOrDefault(s => s.Peer == peer)?.Name ?? "?";

    private void AddLine(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
        Revision++;
    }

    private void TakeChunk(NetMessage.FileChunk chunk)
    {
        if (chunk.Kind != 0 || Data is null || !Data.Map.IsCustom || HasMap) return;   // nothing to fetch when the map is already here
        byte[]? bytes;
        try { bytes = _receiver.Accept(chunk); }
        catch (InvalidDataException) { return; }
        Revision++;
        if (bytes is null) return;
        if (ContentHash.OfBytes(bytes) != Data.Map.FileHash) { AddLine("The map that arrived does not match the host's. Waiting for a new copy."); return; }
        try
        {
            Directory.CreateDirectory(_downloadedDir);
            File.WriteAllBytes(Data.Map.DownloadPath(_downloadedDir), bytes);
            AddLine("Downloaded the map " + MapPicker.LabelOf(Data.Map.File!) + ".");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { AddLine("Could not save the downloaded map: " + e.Message); }
    }

    private void TakeStart(NetMessage.Start start)
    {
        SetupData setup;
        try { setup = SetupCodec.Decode(start.SetupBlob); }
        catch (InvalidDataException) { Leave("The host started a game this version does not understand."); return; }
        MapChoice? map = setup.Map.Resolve(_userMapsDir, _downloadedDir);
        bool member = setup.PeerSlots.ContainsKey(LocalPeer) || setup.Spectators.Contains(LocalPeer);
        if (map is null || !member) { Leave(map is null ? "The match started but the map is missing here." : "The match started without you."); return; }
        Setup = setup.ToMatch(_transport, isHost: false, LocalPeer, map);
        End(LobbyClientState.Started, "");
    }

    // ---- what the client can ask for ----

    /// <summary>Asks the host to change our slot (faction, colour, team, or a move to an open slot).</summary>
    public void Request(SlotChange change)
    {
        if (State == LobbyClientState.InLobby) _transport.Send(0, new NetMessage.SlotRequest(change.Encode()));
    }

    /// <summary>Sets our ready flag. Ignored while we do not have the map.</summary>
    public void SetReady(bool ready)
    {
        if (State == LobbyClientState.InLobby && (!ready || HasMap)) _transport.Send(0, new NetMessage.Ready(ready));
    }

    /// <summary>Sends a chat line to everyone.</summary>
    public void Say(string text)
    {
        text = (text ?? "").Trim();
        if (State == LobbyClientState.InLobby && text.Length > 0) _transport.Send(0, new NetMessage.Chat(LocalPeer, 0, text.Length > NetCodec.MaxChatChars ? text[..NetCodec.MaxChatChars] : text));
    }

    /// <summary>Leaves the lobby: tells the host and ends the connection.</summary>
    public void Leave(string reason = "")
    {
        if (State is LobbyClientState.Connecting or LobbyClientState.InLobby)
        {
            if (_transport is UdpTransport udp) udp.Disconnect(0, "left");
            else _transport.Send(0, new NetMessage.Bye("left"));
            End(LobbyClientState.Disconnected, reason);
        }
    }
}
