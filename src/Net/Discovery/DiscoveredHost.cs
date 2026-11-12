// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Net;

namespace Rts.Net.Discovery;

/// <summary>One host that answered a probe: where to connect (reply source address, announced game port) and what it said.</summary>
public sealed record DiscoveredHost(IPEndPoint GameEndPoint, DiscoveryInfo Info);
