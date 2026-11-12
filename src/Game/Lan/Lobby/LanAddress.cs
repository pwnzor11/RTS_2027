// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;
using System.Net.Sockets;

namespace Rts.Game.Lan.Lobby;

/// <summary>Reads the "Join by IP" text of the LAN browser (M9-05): an IPv4 address with an optional <c>:port</c>, or <c>localhost</c>.</summary>
public static class LanAddress
{
    /// <summary>Parses "192.168.1.20" or "192.168.1.20:47270". The default port is used when none is given. Returns false for anything else.</summary>
    public static bool TryParse(string? text, int defaultPort, out IPEndPoint endPoint)
    {
        endPoint = new IPEndPoint(IPAddress.Loopback, defaultPort);
        text = text?.Trim() ?? "";
        if (text.Length == 0) return false;
        string host = text;
        int port = defaultPort;
        int colon = text.LastIndexOf(':');
        if (colon >= 0)
        {
            host = text[..colon];
            if (!int.TryParse(text[(colon + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535) return false;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) { endPoint = new IPEndPoint(IPAddress.Loopback, port); return true; }
        if (host.Count(c => c == '.') != 3 || !IPAddress.TryParse(host, out IPAddress? ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        endPoint = new IPEndPoint(ip, port);
        return true;
    }
}
