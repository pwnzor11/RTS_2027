// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Scripting;

/// <summary>The "mission" state part: delegates to the host, which owns the data and the Lua state table.</summary>
internal sealed class MissionStatePart : ISimStatePart
{
    private readonly MissionHost _host;

    public MissionStatePart(MissionHost host) => _host = host;

    public string Name => "mission";

    public void Write(BinaryWriter w) => _host.WriteState(w);

    public void Read(BinaryReader r) => _host.ReadState(r);
}
