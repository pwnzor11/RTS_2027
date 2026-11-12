// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rts.Data;
using Rts.Game.Input;
using Rts.Game.Lan;
using Rts.Game.Lan.Lobby;
using Rts.Net;
using Rts.Net.Discovery;
using Rts.Net.Udp;

namespace Rts.Game.Screens;

/// <summary>
/// The LAN browser (M9-05, design 20 §9): the player's name (typed, saved), Host game, a live list of games found on the network (a probe every
/// two seconds), and Join by IP. Hosting opens the game port and the lobby; joining connects and opens the lobby as a guest.
/// </summary>
public sealed class LanBrowserScreen : IScreen
{
    private const long ProbeEveryMicros = 2_000_000;
    private const int ProbeWindowMs = 1_200;

    private readonly GameServices _s;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private MenuPanel _panel;
    private Point _mouse;
    private DiscoveryProbe? _probe;
    private long _probeAt = long.MinValue;
    private bool _harvested = true;
    private IReadOnlyList<DiscoveredHost> _hosts = Array.Empty<DiscoveredHost>();
    private string _name;
    private string _address = "";
    private string _status = "";
    private int _nameRow, _addressRow;
    private bool _nameChanged;
    private bool _spectator;

    /// <summary>Creates the browser; the player name starts as the saved one, else the Windows user name.</summary>
    public LanBrowserScreen(GameServices services)
    {
        _s = services;
        _name = LobbyNames.Resolve(services.Settings.PlayerName);
        _panel = BuildPanel();
    }

    /// <inheritdoc />
    public bool BlocksUpdateBelow => true;

    /// <inheritdoc />
    public bool BlocksDrawBelow => true;

    /// <inheritdoc />
    public void Enter()
    {
        try { _probe = new DiscoveryProbe(_s.Settings.LanDiscoveryPort); }
        catch (SocketException e) { _status = "Cannot look for games: " + e.Message; Rebuild(); }
    }

    /// <inheritdoc />
    public void Exit()
    {
        SaveName();
        _probe?.Dispose();
        _probe = null;
    }

    private long NowMicros => _clock.ElapsedTicks * 1_000_000L / Stopwatch.Frequency;

    /// <inheritdoc />
    public void Update(InputFrame input, GameTime t)
    {
        _mouse = input.MousePosition;
        PollProbe();
        if (input.ConsumePressed(Keys.Escape)) { _s.Screens.Pop(); return; }
        if (_panel.Selected == _nameRow) Type(input, ref _name, LobbyData.MaxNameBytes, ref _nameChanged);
        else if (_panel.Selected == _addressRow) { bool unused = false; Type(input, ref _address, 21, ref unused); }
        _panel.Update(input, _s.Viewport, _s.UiScale);
    }

    private static void Type(InputFrame input, ref string text, int max, ref bool changed)
    {
        foreach (char ch in input.ConsumeText())
        {
            if (ch < ' ' || ch == 127 || text.Length >= max) continue;
            text += ch;
            changed = true;
        }

        if (input.ConsumePressed(Keys.Back) && text.Length > 0) { text = text[..^1]; changed = true; }
    }

    private void PollProbe()
    {
        if (_probe is null) return;
        long now = NowMicros;
        if (_probeAt == long.MinValue || (_probe.IsComplete && now - _probeAt >= ProbeEveryMicros))
        {
            _probeAt = now;
            _harvested = false;
            _probe.Start(now, ProbeWindowMs);
        }

        _probe.Poll(now);
        if (_probe.IsComplete && !_harvested)
        {
            _harvested = true;
            var found = _probe.Results.ToList();
            if (!found.SequenceEqual(_hosts)) { _hosts = found; Rebuild(); }
        }
    }

    /// <inheritdoc />
    public void Draw(SpriteBatch sb)
    {
        Backdrop.FillOpaque(sb, _s);
        _panel.Draw(sb, _s.Pixel, _s.Viewport, _s.UiScale, _mouse);
    }

    private MenuPanel BuildPanel()
    {
        var rows = new List<MenuItem>();
        _nameRow = rows.Count;
        rows.Add(new(() => $"Your name: {_name}_", null) { WidthTexts = new[] { "Your name: " + new string('W', LobbyData.MaxNameBytes) } });
        rows.Add(new(() => "Host game", HostGame));
        rows.Add(new(() => _hosts.Count == 0 ? "Looking for games on the network ..." : "Games found (Enter joins):") { Info = true });
        foreach (DiscoveredHost host in _hosts)
        {
            DiscoveredHost h = host;
            rows.Add(new(() => LobbyText.HostLine(h), () => { if (LobbyText.CanJoin(h)) Join(h.GameEndPoint); else _status = "That game cannot be joined now."; }));
        }

        rows.Add(new(() => _spectator ? "Join as: spectator" : "Join as: player", () => _spectator = !_spectator, d => _spectator = !_spectator,
            new MenuButton("Change", () => _spectator = !_spectator)) { WidthTexts = new[] { "Join as: spectator" } });
        _addressRow = rows.Count;
        rows.Add(new(() => $"Join by IP: {_address}_  (Enter joins)", JoinTyped) { WidthTexts = new[] { "Join by IP: 255.255.255.255:65535_  (Enter joins)" } });
        rows.Add(new(() => _status.Length > 0 ? _status : "Type an address like 192.168.1.20 or 192.168.1.20:47270") { Info = true });
        rows.Add(new(() => "Back", () => _s.Screens.Pop()));
        var panel = new MenuPanel("LAN Game", rows);
        if (_panel is not null) panel.Selected = Math.Min(_panel.Selected, rows.Count - 1);
        return panel;
    }

    private void Rebuild() => _panel = BuildPanel();

    private void SaveName()
    {
        string clean = LobbyNames.Clean(_name);
        if (!_nameChanged || clean.Length == 0) return;
        _nameChanged = false;
        _s.Settings = _s.Settings with { PlayerName = clean };
        _s.Settings.Save();
    }

    private string PlayerName => LobbyNames.Resolve(_name);

    private void HostGame()
    {
        SaveName();
        UdpTransport transport;
        try { transport = UdpTransport.Host(_s.Settings.LanGamePort); }
        catch (SocketException e)
        {
            _status = $"Cannot open port {_s.Settings.LanGamePort}: {e.Message}";
            return;
        }

        DiscoveryResponder? responder = null;
        try { responder = new DiscoveryResponder(new DiscoveryInfo(DiscoveryInfo.GameName, NetProtocol.Version, PlayerName, 1, 6, _s.Settings.LanGamePort, true, false), _s.Settings.LanDiscoveryPort); }
        catch (SocketException) { /* the game can still be joined by IP */ }
        var host = new LobbyHost(transport, PlayerName, ContentHash.ForRulesDir(_s.RulesDir), BuildInfo.Version, LobbyMap.Curated,
            MapPicker.UserMapsDir, DownloadedMapsDir);
        _s.Screens.Push(new LobbyScreen(_s, host, responder, _s.Settings.LanGamePort));
    }

    /// <summary>Where maps downloaded from a host are kept: <c>%APPDATA%/RTS_2027/maps/downloaded</c> (Q-M9-k).</summary>
    public static string DownloadedMapsDir => Path.Combine(MapPicker.UserMapsDir, "downloaded");

    private void JoinTyped()
    {
        if (!LanAddress.TryParse(_address, _s.Settings.LanGamePort, out IPEndPoint ep)) { _status = "That is not an address. Try 192.168.1.20"; return; }
        Join(ep);
    }

    private void Join(IPEndPoint host)
    {
        SaveName();
        try
        {
            var hello = LobbyClient.MakeHello(PlayerName, spectator: _spectator, ContentHash.ForRulesDir(_s.RulesDir), BuildInfo.Version);
            _status = "";
            _s.Screens.Push(new LobbyScreen(_s, LobbyClient.Join(host, hello, MapPicker.UserMapsDir, DownloadedMapsDir)));
        }
        catch (SocketException e)
        {
            _status = "Cannot connect: " + e.Message;
        }
    }
}
