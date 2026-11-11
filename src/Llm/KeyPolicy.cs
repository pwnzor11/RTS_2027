// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;

namespace Rts.Llm;

/// <summary>
/// Where an API key may be sent (S18 AI-07): over https to any host, or over plain http only to this machine (localhost or a loopback
/// address, for a local model server). Anything else would put the key on the network in clear text.
/// </summary>
public static class KeyPolicy
{
    /// <summary>The reason a provider gives when it refuses to send a key (short, no URL, no key).</summary>
    public const string RefusedReason = "the key is not sent over plain http except to localhost (use https)";

    /// <summary>True when a request to <paramref name="baseUrl"/> may carry an API key. An empty URL means the provider's default
    /// (<paramref name="defaultUrl"/>). A URL that is not an absolute http or https address never gets a key.</summary>
    public static bool MaySendKey(string? baseUrl, string defaultUrl)
    {
        string url = string.IsNullOrWhiteSpace(baseUrl) ? defaultUrl : baseUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)) return false;
        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        return uri.Scheme == Uri.UriSchemeHttp && IsLoopbackHost(uri.Host);
    }

    /// <summary>True for "localhost" (and its subdomains) and for loopback IP addresses (127.0.0.0/8, ::1).</summary>
    public static bool IsLoopbackHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)) return true;
        string bare = host.Trim('[', ']');
        return IPAddress.TryParse(bare, out IPAddress? ip) && IPAddress.IsLoopback(ip);
    }
}
