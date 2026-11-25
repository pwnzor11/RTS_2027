// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Rts.Net.Discovery;

/// <summary>
/// Host side of LAN discovery: answers probes arriving on the discovery port (unicast, broadcast or the multicast group
/// <see cref="DiscoveryInfo.MulticastGroup"/>) with the current <see cref="Info"/>. Non-blocking, no threads: call <see cref="Poll"/>.
/// Only private, link-local and loopback IPv4 sources on a local subnet are answered, and each source gets at most one reply per
/// <see cref="MinReplyIntervalMs"/>, so the responder is neither a user-name leak to the internet nor a UDP reflector (NETLLM-09).
/// The reply carries <see cref="DiscoveryInfo.LobbyName"/> only: the caller passes the player's chosen display name.
/// </summary>
public sealed class DiscoveryResponder : IDisposable
{
    private readonly Socket _socket;
    private readonly byte[] _buf = new byte[512];
    private readonly Dictionary<IPAddress, long> _lastReply = new();

    /// <summary>The least time between two replies to the same source address.</summary>
    public const int MinReplyIntervalMs = 1000;

    private const int MaxTrackedSources = 256;

    /// <summary>Opens the responder on <paramref name="port"/> (0 = any free port, for tests) and joins the multicast group when the system allows it.</summary>
    public DiscoveryResponder(DiscoveryInfo info, int port = NetProtocol.DefaultDiscoveryPort, IPAddress? bindAddress = null)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false };
        try
        {
            _socket.Bind(new IPEndPoint(bindAddress ?? IPAddress.Any, port));
            try { _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(DiscoveryInfo.MulticastGroup))); }
            catch (SocketException) { }   // no multicast on this machine: broadcast and unicast still work
        }
        catch { _socket.Dispose(); throw; }
    }

    /// <summary>What replies say; the owner updates it as the lobby changes.</summary>
    public DiscoveryInfo Info { get; set; }

    /// <summary>The bound endpoint (learn the port when opened with 0).</summary>
    public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint!;

    /// <summary>Answers every probe that has arrived. Returns how many were answered.</summary>
    public int Poll()
    {
        int answered = 0;
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
            if (!DiscoveryPacket.IsProbe(_buf.AsSpan(0, n))) continue;
            if (from is not IPEndPoint source || !IsAllowedSource(source.Address)) continue;
            if (!RateAllows(source.Address)) continue;
            byte[] reply = DiscoveryPacket.EncodeReply(Info);
            try { _socket.SendTo(reply, from); answered++; }
            catch (SocketException) { }
        }
        return answered;
    }

    /// <summary>
    /// True for a source the responder may answer: IPv4 loopback, RFC 1918 private (10/8, 172.16/12, 192.168/16) or link-local
    /// (169.254/16), and, unless loopback, inside the subnet of one of this machine's interfaces.
    /// </summary>
    public static bool IsAllowedSource(IPAddress source) => IsAllowedSource(source, LocalSubnets());

    /// <summary>As <see cref="IsAllowedSource(IPAddress)"/> with an explicit list of local (address, mask) pairs.</summary>
    public static bool IsAllowedSource(IPAddress source, IReadOnlyList<(IPAddress Address, IPAddress Mask)> localSubnets)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(localSubnets);
        if (source.IsIPv4MappedToIPv6) source = source.MapToIPv4();
        if (source.AddressFamily != AddressFamily.InterNetwork) return false;
        byte[] a = source.GetAddressBytes();
        if (a[0] == 127) return true;
        bool privateRange = a[0] == 10 || (a[0] == 172 && (a[1] & 0xF0) == 16) || (a[0] == 192 && a[1] == 168) || (a[0] == 169 && a[1] == 254);
        if (!privateRange) return false;
        foreach ((IPAddress addr, IPAddress mask) in localSubnets)
        {
            byte[] l = addr.GetAddressBytes(), m = mask.GetAddressBytes();
            if (l.Length != 4 || m.Length != 4) continue;
            bool same = true;
            for (int i = 0; i < 4 && same; i++) same = (a[i] & m[i]) == (l[i] & m[i]);
            if (same) return true;
        }
        return false;
    }

    private static List<(IPAddress Address, IPAddress Mask)> LocalSubnets()
    {
        var list = new List<(IPAddress, IPAddress)>();
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation u in ni.GetIPProperties().UnicastAddresses)
                    if (u.Address.AddressFamily == AddressFamily.InterNetwork && u.IPv4Mask is { } mask) list.Add((u.Address, mask));
            }
        }
        catch (NetworkInformationException) { }
        return list;
    }

    private bool RateAllows(IPAddress source)
    {
        long now = Stopwatch.GetTimestamp();
        long gap = Stopwatch.Frequency * MinReplyIntervalMs / 1000;
        if (_lastReply.TryGetValue(source, out long last) && now - last < gap) return false;
        if (_lastReply.Count >= MaxTrackedSources)
        {
            foreach (IPAddress k in _lastReply.Where(kv => now - kv.Value >= gap).Select(kv => kv.Key).ToArray()) _lastReply.Remove(k);
            if (_lastReply.Count >= MaxTrackedSources) return false;
        }
        _lastReply[source] = now;
        return true;
    }

    /// <inheritdoc />
    public void Dispose() => _socket.Dispose();
}
