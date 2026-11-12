// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Net.Sockets;

namespace Rts.Net.Discovery;

/// <summary>
/// Host side of LAN discovery: answers probes arriving on the discovery port (unicast, broadcast or the multicast group
/// <see cref="DiscoveryInfo.MulticastGroup"/>) with the current <see cref="Info"/>. Non-blocking, no threads: call <see cref="Poll"/>.
/// </summary>
public sealed class DiscoveryResponder : IDisposable
{
    private readonly Socket _socket;
    private readonly byte[] _buf = new byte[512];

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
            byte[] reply = DiscoveryPacket.EncodeReply(Info);
            try { _socket.SendTo(reply, from); answered++; }
            catch (SocketException) { }
        }
        return answered;
    }

    /// <inheritdoc />
    public void Dispose() => _socket.Dispose();
}
