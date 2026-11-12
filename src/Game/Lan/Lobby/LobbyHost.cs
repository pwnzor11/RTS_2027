// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Screens;
using Rts.Game.Sprites;
using Rts.Net;
using Rts.Net.Discovery;
using Rts.Net.Udp;
using Rts.Sim.Rules;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// The host side of a LAN lobby (M9-05, M9-06, M9-13), without any screen: it owns the lobby state, checks each joiner's Hello
/// (<see cref="Handshake"/>), applies slot requests, ready flags and chat, sends a custom map to peers, and builds the final
/// <see cref="LanMatchSetup"/> on <see cref="Start"/>. The screen calls <see cref="Poll"/> every frame and the edit methods from its buttons.
/// Works over <see cref="UdpTransport"/> (pending peers are accepted or rejected) and over the in-process loopback transport (tests).
/// </summary>
public sealed class LobbyHost
{
    private const int MaxLines = 60;

    private readonly INetTransport _transport;
    private readonly UdpTransport? _udp;
    private readonly string _buildVersion;
    private readonly ulong _contentHash;
    private readonly string _userMapsDir;
    private readonly string _downloadedDir;
    private readonly Func<ulong> _seedSource;
    private readonly List<string> _lines = new();
    private LobbyData _data;
    private int _nextPeer = 1;
    private int _nextTransfer;
    private bool _started;

    /// <summary>Creates the host side over a transport that is already listening.</summary>
    /// <param name="transport">The host transport (peer 0).</param>
    /// <param name="hostName">The host's display name.</param>
    /// <param name="contentHash">The host's <see cref="ContentHash"/>; joiners must match it.</param>
    /// <param name="buildVersion">The host's <c>BuildInfo.Version</c>; joiners must match it.</param>
    /// <param name="map">The first map.</param>
    /// <param name="userMapsDir">The player's own maps folder.</param>
    /// <param name="downloadedDir">Where downloaded maps are kept.</param>
    /// <param name="seedSource">The simulation seed for a map file (a generated map uses its own seed); null = a random one.</param>
    public LobbyHost(INetTransport transport, string hostName, ulong contentHash, string buildVersion, LobbyMap map,
        string userMapsDir, string downloadedDir, Func<ulong>? seedSource = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _udp = transport as UdpTransport;
        _contentHash = contentHash;
        _buildVersion = buildVersion;
        _userMapsDir = userMapsDir;
        _downloadedDir = downloadedDir;
        _seedSource = seedSource ?? (() => (ulong)Random.Shared.NextInt64(1, 1_000_000_000));
        _data = Initial(LobbyNames.Resolve(hostName), map);
    }

    /// <summary>The transport (the screen disposes it when the host leaves before the match).</summary>
    public INetTransport Transport => _transport;

    /// <summary>The lobby as it stands.</summary>
    public LobbyData Data => _data;

    /// <summary>Bumped on every change, so a screen can tell when to redraw its rows.</summary>
    public int Revision { get; private set; }

    /// <summary>Status and chat lines, oldest first (the last <c>60</c> are kept).</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>True once <see cref="Start"/> has run.</summary>
    public bool Started => _started;

    /// <summary>The first lobby: the host in slot 0, an open slot, and two closed ones.</summary>
    public static LobbyData Initial(string hostName, LobbyMap map) => new(hostName, map, new[]
    {
        new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Authority, SkirmishSettings.DefaultAiTier, 0, 0), 0, hostName, true),
        new LobbySlotState(new SkirmishSlot(SlotKind.Human, Faction.Authority, SkirmishSettings.DefaultAiTier, 1, 1)),
        new LobbySlotState(new SkirmishSlot(SlotKind.Closed, Faction.Authority, SkirmishSettings.DefaultAiTier, 2, 2)),
        new LobbySlotState(new SkirmishSlot(SlotKind.Closed, Faction.Authority, SkirmishSettings.DefaultAiTier, 3, 3)),
    }, Array.Empty<LobbySpectator>(), NetProtocol.DefaultDelay);

    /// <summary>What a LAN probe is told about this lobby.</summary>
    public DiscoveryInfo Announcement(int gamePort) => new(DiscoveryInfo.GameName, NetProtocol.Version, _data.HostName,
        _data.People, _data.Slots.Count(s => s.Slot.Kind == SlotKind.Human) + NetProtocol.MaxSpectators, gamePort,
        !_started && (_data.OpenSlots > 0 || _data.Spectators.Count < NetProtocol.MaxSpectators), _started);

    // ---- the network side ----

    /// <summary>Pumps the transport and handles what arrived.</summary>
    public void Poll(long nowMicroseconds)
    {
        _transport.Poll(nowMicroseconds);
        while (_transport.TryReceive(out int from, out NetMessage message)) Handle(from, message);
        while (_transport.TryGetEvent(out PeerEvent e))
            if (!e.Connected && e.Peer > 0) RemovePeer(e.Peer, e.Reason);
    }

    private void Handle(int from, NetMessage message)
    {
        if (message is NetMessage.Hello hello)
        {
            if (from < 0 || !IsMember(from)) HandleHello(from, hello);
            return;
        }

        if (!IsMember(from)) return;
        switch (message)
        {
            case NetMessage.SlotRequest r:
                try { ApplyRequest(from, SlotChange.Decode(r.Blob)); }
                catch (InvalidDataException) { /* a bad request is ignored */ }
                break;
            case NetMessage.Ready r: SetReady(from, r.IsReady); break;
            case NetMessage.Chat c: Say(from, c.Text); break;
            case NetMessage.Ping p: _transport.Send(from, new NetMessage.Pong(p.Stamp), reliable: false); break;
            case NetMessage.Bye b: RemovePeer(from, b.Reason); break;
        }
    }

    private bool IsMember(int peer) => peer > 0 && (_data.SlotOf(peer) >= 0 || _data.Spectators.Any(s => s.Peer == peer));

    /// <summary>
    /// Checks a joiner's Hello in this order: protocol, game version, game data, game started, lobby full. Returns null to accept or the exact
    /// reason (both versions shown) to reject.
    /// </summary>
    public string? Handshake(NetMessage.Hello hello)
    {
        if (hello.Protocol != NetProtocol.Version)
            return $"Network protocol differs: you have {hello.Protocol}, the host has {NetProtocol.Version}. Install the same game version as the host.";
        if (hello.BuildVersion != _buildVersion)
            return $"Game version differs: you have {hello.BuildVersion}, the host has {_buildVersion}. Install the same game version as the host.";
        if (hello.ContentHash != _contentHash)
            return $"Game data differs: yours is {hello.ContentHash:x16}, the host's is {_contentHash:x16}. Use the same game version and mods as the host.";
        if (_started) return "The game has already started.";
        if (hello.Spectator)
            return _data.Spectators.Count >= NetProtocol.MaxSpectators ? "The lobby has no spectator places left." : null;
        return _data.OpenSlots == 0 ? "The lobby is full." : null;
    }

    private void HandleHello(int from, NetMessage.Hello hello)
    {
        string? reason = Handshake(hello);
        if (reason is not null)
        {
            if (_udp is not null && from < 0) _udp.Reject(from, reason);
            else _transport.Send(from, new NetMessage.Reject(reason));
            return;
        }

        int id = from;
        if (from < 0)
        {
            id = _nextPeer++;
            if (_udp is null || !_udp.Accept(from, id)) return;
        }
        else _nextPeer = Math.Max(_nextPeer, from + 1);

        var taken = _data.Slots.Where(s => s.IsTaken).Select(s => s.Name).Concat(_data.Spectators.Select(s => s.Name)).ToList();
        string name = LobbyNames.Unique(LobbyNames.Resolve(hello.PlayerName, LobbyNames.Fallback), taken);
        if (hello.Spectator)
            _data = _data with { Spectators = _data.Spectators.Append(new LobbySpectator(id, name)).ToList() };
        else
        {
            int slot = _data.Slots.ToList().FindIndex(s => s.IsOpen);
            var slots = _data.Slots.ToList();
            slots[slot] = slots[slot] with { Peer = id, Name = name, Ready = false };
            _data = _data with { Slots = slots };
        }

        _transport.Send(id, new NetMessage.Welcome(id));
        AddLine(name + (hello.Spectator ? " joined as a spectator." : " joined."));
        Changed();
        SendMap(id);
    }

    private void Changed()
    {
        Revision++;
        _transport.Broadcast(new NetMessage.LobbyState(LobbyCodec.Encode(_data)));
    }

    private void AddLine(string line)
    {
        _lines.Add(line);
        if (_lines.Count > MaxLines) _lines.RemoveAt(0);
        Revision++;
    }

    private void RemovePeer(int peer, string reason)
    {
        int slot = _data.SlotOf(peer);
        string? name = null;
        if (slot >= 0)
        {
            var slots = _data.Slots.ToList();
            name = slots[slot].Name;
            slots[slot] = slots[slot] with { Peer = LobbySlotState.NoPeer, Name = "", Ready = false };
            _data = _data with { Slots = slots };
        }
        else if (_data.Spectators.FirstOrDefault(s => s.Peer == peer) is { } spec)
        {
            name = spec.Name;
            _data = _data with { Spectators = _data.Spectators.Where(s => s.Peer != peer).ToList() };
        }

        if (name is null) return;
        AddLine($"{name} left" + (string.IsNullOrEmpty(reason) ? "." : $" ({reason})."));
        Changed();
    }

    // ---- chat, ready, requests ----

    /// <summary>Chat from the host or a peer: relayed to everyone and kept in <see cref="Lines"/>.</summary>
    public void Say(int fromPeer, string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) return;
        if (text.Length > NetCodec.MaxChatChars) text = text[..NetCodec.MaxChatChars];
        string name = fromPeer == 0 ? _data.HostName
            : _data.Slots.FirstOrDefault(s => s.IsTaken && s.Peer == fromPeer)?.Name ?? _data.Spectators.FirstOrDefault(s => s.Peer == fromPeer)?.Name ?? "?";
        AddLine($"{name}: {text}");
        _transport.Broadcast(new NetMessage.Chat(fromPeer, 0, text));
    }

    private void SetReady(int peer, bool ready)
    {
        int slot = _data.SlotOf(peer);
        if (slot < 0 || _data.Slots[slot].Ready == ready) return;
        var slots = _data.Slots.ToList();
        slots[slot] = slots[slot] with { Ready = ready };
        _data = _data with { Slots = slots };
        Changed();
    }

    private void ApplyRequest(int peer, SlotChange change)
    {
        int slot = _data.SlotOf(peer);
        if (slot < 0) return;
        var slots = _data.Slots.ToList();
        if (change.MoveTo >= 0)
        {
            if (change.MoveTo >= slots.Count || !slots[change.MoveTo].IsOpen) return;
            LobbySlotState me = slots[slot];
            slots[change.MoveTo] = slots[change.MoveTo] with { Peer = me.Peer, Name = me.Name, Ready = false };
            slots[slot] = slots[slot] with { Peer = LobbySlotState.NoPeer, Name = "", Ready = false };
            slot = change.MoveTo;
        }

        SkirmishSlot s = slots[slot].Slot;
        int colour = change.Colour;
        bool colourTaken = slots.Where((x, i) => i != slot && x.Slot.Kind != SlotKind.Closed).Any(x => x.Slot.Colour == colour);
        if (colourTaken) colour = s.Colour;
        if (!RuleCounts.IsPlayable(change.Faction)) return;
        slots[slot] = slots[slot] with { Slot = s with { Faction = change.Faction, Colour = colour, Team = change.Team }, Ready = false };
        _data = _data with { Slots = slots };
        Changed();
    }

    // ---- host edits ----

    /// <summary>Sets what slot <paramref name="index"/> (not the host's own, slot 0) is: Human (open), Ai or Closed. A player in it is sent away.</summary>
    public void SetSlotKind(int index, SlotKind kind)
    {
        if (index < 1 || index >= _data.Slots.Count || _started) return;
        var slots = _data.Slots.ToList();
        LobbySlotState cur = slots[index];
        if (cur.Slot.Kind == kind) return;
        if (cur.IsTaken) Kick(cur.Peer, "The host closed your slot.");
        slots = _data.Slots.ToList();
        SkirmishSlot s = slots[index].Slot with { Kind = kind };
        if (kind != SlotKind.Closed && slots.Where((x, i) => i != index && x.Slot.Kind != SlotKind.Closed).Any(x => x.Slot.Colour == s.Colour))
            s = s with { Colour = FreeColour(slots, index) };
        slots[index] = new LobbySlotState(s);
        _data = _data with { Slots = slots };
        Changed();
    }

    /// <summary>Replaces the slot choices (faction, tier, team, colour, options) of slot <paramref name="index"/>; a player in it must ready again. The kind is not changed here.</summary>
    public void UpdateSlot(int index, Func<SkirmishSlot, SkirmishSlot> change)
    {
        if (index < 0 || index >= _data.Slots.Count || _started) return;
        var slots = _data.Slots.ToList();
        SkirmishSlot before = slots[index].Slot;
        SkirmishSlot after = change(before) with { Kind = before.Kind };
        if (after == before) return;
        slots[index] = slots[index] with { Slot = after, Ready = index == 0 && slots[index].Ready };
        _data = _data with { Slots = slots };
        Changed();
    }

    /// <summary>Moves slot <paramref name="index"/> to the next colour nobody else uses.</summary>
    public void CycleColour(int index)
    {
        if (index < 0 || index >= _data.Slots.Count) return;
        var slots = _data.Slots.ToList();
        for (int k = 1; k <= PlayerColours.Count; k++)
        {
            int c = (slots[index].Slot.Colour + k) % PlayerColours.Count;
            if (slots.Where((x, i) => i != index && x.Slot.Kind != SlotKind.Closed).Any(x => x.Slot.Colour == c)) continue;
            UpdateSlot(index, s => s with { Colour = c });
            return;
        }
    }

    private static int FreeColour(List<LobbySlotState> slots, int except)
    {
        for (int c = 0; c < PlayerColours.Count; c++)
            if (!slots.Where((x, i) => i != except && x.Slot.Kind != SlotKind.Closed).Any(x => x.Slot.Colour == c)) return c;
        return slots[except].Slot.Colour;
    }

    /// <summary>Sends a player away (Bye with <paramref name="reason"/>) and frees their slot.</summary>
    public void Kick(int peer, string reason = "You were removed by the host.")
    {
        if (!IsMember(peer)) return;
        if (_udp is not null) _udp.Disconnect(peer, reason);
        else _transport.Send(peer, new NetMessage.Bye(reason));
        RemovePeer(peer, "removed");
    }

    /// <summary>Changes the map. A player's own map must be on this machine; returns false (and changes nothing) when it is not or is too large to send.</summary>
    public bool SetMap(LobbyMap map)
    {
        if (_started) return false;
        if (map.IsCustom)
        {
            string? path = map.FindCustom(_userMapsDir, _downloadedDir);
            if (path is null) { AddLine("That map file is not here."); return false; }
            if (new FileInfo(path).Length > MapReceiver.MaxBytes) { AddLine("That map is too large to send over the network."); return false; }
        }

        if (map == _data.Map) return true;
        _data = _data with { Map = map };
        foreach (int i in Enumerable.Range(0, _data.Slots.Count)) if (_data.Slots[i].IsTaken && _data.Slots[i].Peer != 0) UnreadyForMap(i);
        Changed();
        foreach (LobbySlotState s in _data.Slots) if (s.IsTaken && s.Peer != 0) SendMap(s.Peer);
        foreach (LobbySpectator sp in _data.Spectators) SendMap(sp.Peer);
        return true;
    }

    private void UnreadyForMap(int slot)
    {
        var slots = _data.Slots.ToList();
        slots[slot] = slots[slot] with { Ready = false };
        _data = _data with { Slots = slots };
    }

    /// <summary>Sets the command delay (2, 3 or 4 ticks).</summary>
    public void SetDelay(int ticks)
    {
        if (_started || !NetProtocol.DelayChoices.Contains(ticks) || ticks == _data.CommandDelay) return;
        _data = _data with { CommandDelay = ticks };
        Changed();
    }

    private void SendMap(int peer)
    {
        if (!_data.Map.IsCustom) return;
        string? path = _data.Map.FindCustom(_userMapsDir, _downloadedDir);
        if (path is null) return;
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }
        foreach (NetMessage.FileChunk chunk in MapChunker.Split(bytes, ++_nextTransfer)) _transport.Send(peer, chunk);
    }

    // ---- start ----

    /// <summary>The final settings if the lobby can start, else the first reason it cannot (null reason when it can).</summary>
    public SetupData? Preview(out string? why)
    {
        why = null;
        if (_started) { why = "The game has started."; return null; }
        foreach (LobbySlotState s in _data.Slots)
            if (s.IsTaken && s.Peer != 0 && !s.Ready) { why = $"Waiting for {s.Name} to be ready."; return null; }
        var slots = _data.Slots.Select(s => s.IsOpen ? s.Slot with { Kind = SlotKind.Closed } : s.Slot).ToList();
        int open = slots.Count(s => s.Kind != SlotKind.Closed);
        LobbyMap map = _data.Map;
        if (map.Generated is { } g) map = LobbyMap.FromGenerated(g with { Players = Math.Clamp(open, SkirmishSettings.MinPlayers, SkirmishSettings.MaxSlots) });
        MapChoice? choice = map.Resolve(_userMapsDir, _downloadedDir);
        if (choice is null) { why = "The chosen map file is missing."; return null; }
        ulong seed = map.Generated?.Seed ?? _seedSource();
        var check = new SkirmishSettings(choice, slots, seed);
        IReadOnlyList<string> problems = check.Validate(SkirmishSettings.MaxSlots);
        if (problems.Count > 0) { why = problems[0]; return null; }

        var peerSlots = new Dictionary<int, int>();
        var names = new Dictionary<int, string>();
        for (int i = 0; i < _data.Slots.Count; i++)
            if (_data.Slots[i].IsTaken) { peerSlots[_data.Slots[i].Peer] = i; names[_data.Slots[i].Peer] = _data.Slots[i].Name; }
        foreach (LobbySpectator sp in _data.Spectators) names[sp.Peer] = sp.Name;
        return new SetupData(map, slots, seed, peerSlots, _data.Spectators.Select(s => s.Peer).ToList(), names, _data.CommandDelay);
    }

    /// <summary>True when <see cref="Start"/> would work; <paramref name="why"/> says what is missing otherwise.</summary>
    public bool CanStart(out string? why)
    {
        Preview(out why);
        return why is null;
    }

    /// <summary>
    /// Starts the match: sends Start (delay and setup blob) to every peer and returns the host's own <see cref="LanMatchSetup"/>; null (with
    /// <paramref name="why"/>) when the lobby is not ready. The lobby stops taking joins.
    /// </summary>
    public LanMatchSetup? Start(out string? why)
    {
        SetupData? setup = Preview(out why);
        if (setup is null) return null;
        MapChoice choice = setup.Map.Resolve(_userMapsDir, _downloadedDir)!;
        _started = true;
        _transport.Broadcast(new NetMessage.Start(setup.CommandDelay, SetupCodec.Encode(setup)));
        Revision++;
        return setup.ToMatch(_transport, isHost: true, localPeer: 0, choice);
    }
}
