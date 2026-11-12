// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Rts.Net.Discovery;

/// <summary>
/// Client side of LAN discovery. <see cref="Start"/> sends a probe to every target (default: 255.255.255.255, each interface's directed
/// broadcast and the multicast group) and again after 300 ms in case the first was lost; <see cref="Poll"/> collects replies for the
/// window and de-duplicates them by game endpoint. The target list is injectable so tests can aim at 127.0.0.1.
/// </summary>
public sealed class DiscoveryProbe : IDisposable
{
    private const long ResendAfterMicros = 300_000;
    private readonly Socket _socket;
    private readonly IReadOnlyList<IPEndPoint> _targets;
    private readonly List<DiscoveredHost> _results = new();
    private readonly byte[] _buf = new byte[512];
    private long _startedAt, _windowMicros;
    private bool _started, _resent;

    /// <summary>Creates a probe. <paramref name="targets"/> null means <see cref="DefaultTargets"/> for <paramref name="port"/>.</summary>
    public DiscoveryProbe(int port = NetProtocol.DefaultDiscoveryPort, IReadOnlyList<IPEndPoint>? targets = null)
    {
        _targets = targets ?? DefaultTargets(port);
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
        try
        {
            _socket.EnableBroadcast = true;
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        }
        catch { _socket.Dispose(); throw; }
    }

    /// <summary>Hosts found so far, in arrival order, one per game endpoint.</summary>
    public IReadOnlyList<DiscoveredHost> Results => _results;

    /// <summary>True once the window has passed (call <see cref="Poll"/> with a later time to see it).</summary>
    public bool IsComplete { get; private set; }

    /// <summary>The broadcast, directed-broadcast and multicast endpoints a normal scan uses.</summary>
    public static IReadOnlyList<IPEndPoint> DefaultTargets(int port)
    {
        var list = new List<IPEndPoint> { new(IPAddress.Broadcast, port) };
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation ua in nic.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                    byte[] a = ua.Address.GetAddressBytes(), m = ua.IPv4Mask.GetAddressBytes();
                    var d = new byte[4];
                    for (int i = 0; i < 4; i++) d[i] = (byte)(a[i] | ~m[i]);
                    var ep = new IPEndPoint(new IPAddress(d), port);
                    if (!list.Contains(ep)) list.Add(ep);
                }
            }
        }
        catch (Exception e) when (e is NetworkInformationException or SocketException or PlatformNotSupportedException) { }
        list.Add(new IPEndPoint(IPAddress.Parse(DiscoveryInfo.MulticastGroup), port));
        return list;
    }

    /// <summary>Clears earlier results, sends the probe and opens a collection window of <paramref name="windowMs"/>.</summary>
    public void Start(long nowMicroseconds, int windowMs)
    {
        _results.Clear();
        _startedAt = nowMicroseconds;
        _windowMicros = (long)windowMs * 1000;
        _started = true; _resent = false; IsComplete = false;
        SendProbes();
    }

    /// <summary>Reads replies, resends once, and marks the probe complete when the window is over.</summary>
    public void Poll(long nowMicroseconds)
    {
        if (!_started) return;
        if (!_resent && nowMicroseconds - _startedAt >= ResendAfterMicros) { _resent = true; SendProbes(); }
        for (int i = 0; i < 256; i++)
        {
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            int n;
            try
            {
                if (!_socket.Poll(0, SelectMode.SelectRead)) break;
                n = _socket.ReceiveFrom(_buf, 0, _buf.Length, SocketFlags.None, ref from);
            }
            catch (SocketException) { continue; }
            catch (ObjectDisposedException) { break; }
            if (IsComplete) continue;
            DiscoveryInfo? info = DiscoveryPacket.DecodeReply(_buf.AsSpan(0, n));
            if (info == null) continue;
            var game = new IPEndPoint(((IPEndPoint)from).Address, info.GamePort);
            if (_results.Exists(r => r.GameEndPoint.Equals(game))) continue;
            _results.Add(new DiscoveredHost(game, info));
        }
        if (nowMicroseconds - _startedAt >= _windowMicros) IsComplete = true;
    }

    private void SendProbes()
    {
        byte[] probe = DiscoveryPacket.EncodeProbe();
        foreach (IPEndPoint t in _targets)
        {
            try { _socket.SendTo(probe, t); }
            catch (SocketException) { }   // an interface without a route to that target: skip it
        }
    }

    /// <inheritdoc />
    public void Dispose() => _socket.Dispose();
}
