// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.BeaconDelivery;

/// <summary>
/// The "beacon_orders" state part (S44 M-15): the pending DropBeacon order of each vehicle and each soldier. Appended after "mines".
/// Per actor slot: <see cref="Set"/> while an order waits, the target cell (<see cref="X"/>, <see cref="Y"/>) and the build
/// <see cref="Progress"/> in ticks (a soldier builds over the beacon's build time; a vehicle drops at once, so its progress stays 0).
/// Layout: version, vehicle slot count, infantry slot count, then for each kind the rows (slot, set, x, y, progress) of the slots with any non-default field,
/// 0-terminated.
/// </summary>
public sealed class BeaconOrderState : ISimStatePart
{
    private const byte Version = 1;

    private readonly bool[][] _set = new bool[2][];
    private readonly int[][] _x = new int[2][];
    private readonly int[][] _y = new int[2][];
    private readonly int[][] _progress = new int[2][];

    /// <summary>Creates an empty part with one row per vehicle and per infantry slot.</summary>
    public BeaconOrderState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int[] n = { setup.Limits.Vehicles, setup.Limits.Infantry };
        for (int k = 0; k < 2; k++)
        {
            _set[k] = new bool[n[k]]; _x[k] = new int[n[k]]; _y[k] = new int[n[k]]; _progress[k] = new int[n[k]];
        }
    }

    /// <inheritdoc />
    public string Name => "beacon_orders";

    private static int K(ActorKind kind) => kind == ActorKind.Infantry ? 1 : 0;

    /// <summary>True while the actor has a pending beacon order.</summary>
    public bool IsSet(ActorKind kind, int slot) => _set[K(kind)][slot];

    /// <summary>The order's target cell (meaningful while <see cref="IsSet"/>).</summary>
    public CellCoord Target(ActorKind kind, int slot) => new(_x[K(kind)][slot], _y[K(kind)][slot]);

    /// <summary>Build progress in ticks.</summary>
    public int ProgressOf(ActorKind kind, int slot) => _progress[K(kind)][slot];

    /// <summary>Adds one tick of progress and returns the new value.</summary>
    public int Advance(ActorKind kind, int slot) => ++_progress[K(kind)][slot];

    /// <summary>Stores an order (progress restarts at 0).</summary>
    public void Set(ActorKind kind, int slot, CellCoord target)
    {
        int k = K(kind);
        _set[k][slot] = true; _x[k][slot] = target.X; _y[k][slot] = target.Y; _progress[k][slot] = 0;
    }

    /// <summary>Drops the order and resets the row (slot reuse, completion, cancel).</summary>
    public void Clear(ActorKind kind, int slot)
    {
        int k = K(kind);
        _set[k][slot] = false; _x[k][slot] = 0; _y[k][slot] = 0; _progress[k][slot] = 0;
    }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(_set[0].Length);
        w.Write(_set[1].Length);
        for (int k = 0; k < 2; k++)
        {
            for (int i = 1; i < _set[k].Length; i++)
                if (_set[k][i] || _x[k][i] != 0 || _y[k][i] != 0 || _progress[k][i] != 0)
                { w.Write(i); w.Write(_set[k][i]); w.Write(_x[k][i]); w.Write(_y[k][i]); w.Write(_progress[k][i]); }
            w.Write(0);
        }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("beacon_orders version mismatch.");
        if (r.ReadInt32() != _set[0].Length || r.ReadInt32() != _set[1].Length) throw new InvalidDataException("beacon_orders capacity mismatch.");
        for (int k = 0; k < 2; k++)
        {
            for (int i = 1; i < _set[k].Length; i++) Clear(k == 1 ? ActorKind.Infantry : ActorKind.Vehicle, i);
            for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
            {
                int s = StateIo.Slot(i, _set[k].Length, Name);
                _set[k][s] = r.ReadBoolean(); _x[k][s] = r.ReadInt32(); _y[k][s] = r.ReadInt32(); _progress[k][s] = r.ReadInt32();
            }
        }
    }
}
