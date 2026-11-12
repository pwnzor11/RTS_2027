// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net;

/// <summary>Fixed protocol constants (M9, design 20 §3 and §9). Ports are defaults; settings may override them (§8 item 7).</summary>
public static class NetProtocol
{
    /// <summary>Protocol version in Hello and in discovery replies; bump on any wire change.</summary>
    public const int Version = 1;

    /// <summary>Default UDP port for lobby and match traffic.</summary>
    public const int DefaultGamePort = 47270;

    /// <summary>Default UDP port for LAN discovery (broadcast/multicast probes and replies).</summary>
    public const int DefaultDiscoveryPort = 47271;

    /// <summary>Largest UDP payload we send (no IP fragmentation, design 20 §9).</summary>
    public const int MaxPacketBytes = 1200;

    /// <summary>Command delay choices offered in the lobby (Q-M9-c): 2, 3 or 4 ticks; default 3.</summary>
    public static IReadOnlyList<int> DelayChoices { get; } = new[] { 2, 3, 4 };

    /// <summary>Default command delay in ticks.</summary>
    public const int DefaultDelay = 3;

    /// <summary>Most spectators a lobby admits (Q-M9-l).</summary>
    public const int MaxSpectators = 2;

    /// <summary>A peer that has not been heard from for this long is disconnected.</summary>
    public const int TimeoutMs = 60_000;

    /// <summary>Waiting overlay shows after a peer lags this long (Q-M9-d).</summary>
    public const int WaitOverlayMs = 1_000;

    /// <summary>The host may drop a lagging peer after this long (Q-M9-d).</summary>
    public const int HostMayDropMs = 10_000;

    /// <summary>The host drops a lagging peer automatically after this long (Q-M9-d).</summary>
    public const int AutoDropMs = 60_000;

    /// <summary>Resume countdown (Q-M9-h).</summary>
    public const int ResumeCountdownMs = 3_000;
}
