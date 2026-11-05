// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Shipments;

/// <summary>One supply tender in flight (M7-02 contract; filled by M7-06): who owns it, where it is, and the credits it carries.</summary>
/// <param name="Id">Tender id, unique per game (taken from <see cref="ShipmentState.NextTenderId"/>).</param>
/// <param name="Player">Owning player.</param>
/// <param name="CellX">Cell X.</param>
/// <param name="CellY">Cell Y.</param>
/// <param name="Payload">Credits aboard (the shipment value after decay).</param>
/// <param name="Vehicle">M7-06: the tender's vehicle entity, packed as <c>Index &lt;&lt; 16 | Generation</c> (0 = none, e.g. a hand-made test entry).</param>
public readonly record struct Tender(int Id, byte Player, int CellX, int CellY, int Payload, int Vehicle = 0);

/// <summary>
/// The "shipments" state part (M7-02 skeleton, docs/design/11_m7_plan.md §2.4; filled by M7-06). Appended after "veterancy". Per player
/// (indexed by player number): the tick the next shipment sails (<see cref="NextShipmentTick"/>, 0 = none scheduled), the decay step
/// reached (<see cref="DecayStep"/>) and the charter progress in ticks (<see cref="CharterProgress"/>); plus the tenders in flight, in
/// creation order. The spawn cell of a tender is a deterministic index into the map's ocean-edge cells (no SimRandom). Named
/// "shipments" so it is not confused with the supply cap, which has no state (13 §3). All zero and empty by default.
/// Layout: version, player count, then (nextShipmentTick, decayStep, charterProgress) per player, nextTenderId, tender count and
/// (id, player, cellX, cellY, payload, vehicle) per tender.
/// </summary>
public sealed class ShipmentState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Tick of the next shipment per player (0 = none scheduled).</summary>
    public readonly int[] NextShipmentTick;
    /// <summary>Decay steps taken per player.</summary>
    public readonly int[] DecayStep;
    /// <summary>Ticks of charter progress toward winning back a decay step, per player.</summary>
    public readonly int[] CharterProgress;
    /// <summary>The tenders in flight, in creation order.</summary>
    public readonly List<Tender> Tenders = new();
    /// <summary>Id the next tender takes.</summary>
    public int NextTenderId;

    /// <summary>Creates an empty part for <see cref="SimSetup.PlayerCount"/> players.</summary>
    public ShipmentState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        NextShipmentTick = new int[setup.PlayerCount];
        DecayStep = new int[setup.PlayerCount];
        CharterProgress = new int[setup.PlayerCount];
    }

    /// <inheritdoc />
    public string Name => "shipments";

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(NextShipmentTick.Length);
        for (int p = 0; p < NextShipmentTick.Length; p++) { w.Write(NextShipmentTick[p]); w.Write(DecayStep[p]); w.Write(CharterProgress[p]); }
        w.Write(NextTenderId);
        w.Write(Tenders.Count);
        foreach (Tender t in Tenders) { w.Write(t.Id); w.Write(t.Player); w.Write(t.CellX); w.Write(t.CellY); w.Write(t.Payload); w.Write(t.Vehicle); }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("shipments version mismatch.");
        if (r.ReadInt32() != NextShipmentTick.Length) throw new InvalidDataException("shipments player count mismatch.");
        for (int p = 0; p < NextShipmentTick.Length; p++) { NextShipmentTick[p] = r.ReadInt32(); DecayStep[p] = r.ReadInt32(); CharterProgress[p] = r.ReadInt32(); }
        NextTenderId = r.ReadInt32();
        int n = StateIo.Count(r, Name);
        Tenders.Clear();
        for (int i = 0; i < n; i++)
        {
            var t = new Tender(r.ReadInt32(), r.ReadByte(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
            if (t.Player >= NextShipmentTick.Length) throw new InvalidDataException("shipments tender owner out of range.");
            Tenders.Add(t);
        }
    }
}
