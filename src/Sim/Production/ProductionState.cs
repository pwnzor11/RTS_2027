// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Production;

/// <summary>
/// The "production" state part (M2). Skeleton: the real state is added by the production package; for now it only writes a
/// version byte. Appended after "players" (append-only order).
/// </summary>
public sealed class ProductionState : ISimStatePart
{
    private const byte Version = 1;

    public ProductionState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
    }

    public string Name => "production";

    public void Write(BinaryWriter w) => w.Write(Version);

    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version != Version) throw new InvalidDataException("production version mismatch.");
    }
}
