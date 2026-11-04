// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Tunnels;

/// <summary>
/// The "tunnels" state part (M3-01 skeleton; filled by M3-02 network transit, M3-03 burrowing/deploy, M3-06 transports).
/// Appended after "production" (append-only order). Empty by default.
/// <list type="bullet">
/// <item><see cref="VehicleLayer"/> / <see cref="InfantryLayer"/>: where each mobile slot is (<see cref="ActorLayer"/>,
/// Surface by default). Cleared when an actor spawns or is removed (Simulation.M3.cs).</item>
/// <item><see cref="InfantryCarrier"/>: the transport vehicle an infantry slot rides in (Layer = Passenger), else none (M3-06).</item>
/// <item><see cref="BuildingMorphType"/> / <see cref="BuildingMorphTicks"/>: a building turning into another type (Hatch to Bore
/// Head upgrade, M3-03): target type index + 1 (0 = none) and ticks left. Cleared when the building is removed.</item>
/// <item><see cref="Queue"/>: units waiting at entrances, each entrance's queue in list order (M3-02).</item>
/// <item><see cref="Transit"/>: units inside a network (M3-02).</item>
/// <item><see cref="Telegraphs"/>: surfacing telegraphs in progress (M3-03 units and new entrances).</item>
/// <item><see cref="NextTicket"/>: next queue ticket (starts at 1).</item>
/// </list>
/// Layout: version, capacities, NextTicket, sparse (slot, layer) runs for vehicles then infantry (0-terminated), sparse
/// (slot, carrier) for infantry, sparse (slot, morph type, ticks) for buildings, then each list as count + entries.
/// </summary>
public sealed class TunnelState : ISimStatePart
{
    private const byte Version = 1;

    /// <summary>Layer of each vehicle slot.</summary>
    public readonly ActorLayer[] VehicleLayer;
    /// <summary>Layer of each infantry slot.</summary>
    public readonly ActorLayer[] InfantryLayer;
    /// <summary>Transport (vehicle id) each infantry slot rides in, or none.</summary>
    public readonly EntityId[] InfantryCarrier;
    /// <summary>Per building slot: building type index + 1 this building is morphing into (0 = none).</summary>
    public readonly int[] BuildingMorphType;
    /// <summary>Per building slot: ticks left of the morph.</summary>
    public readonly int[] BuildingMorphTicks;
    /// <summary>Units waiting at entrances.</summary>
    public readonly List<TunnelQueueEntry> Queue = new();
    /// <summary>Units in transit.</summary>
    public readonly List<TunnelTransit> Transit = new();
    /// <summary>Surfacing telegraphs in progress.</summary>
    public readonly List<Telegraph> Telegraphs = new();
    /// <summary>Next queue ticket to hand out.</summary>
    public int NextTicket = 1;

    /// <summary>Creates an empty part sized by the setup's store limits.</summary>
    public TunnelState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        VehicleLayer = new ActorLayer[setup.Limits.Vehicles];
        InfantryLayer = new ActorLayer[setup.Limits.Infantry];
        InfantryCarrier = new EntityId[setup.Limits.Infantry];
        BuildingMorphType = new int[setup.Limits.Buildings];
        BuildingMorphTicks = new int[setup.Limits.Buildings];
    }

    /// <inheritdoc />
    public string Name => "tunnels";

    /// <summary>Layer of a mobile slot (Surface for buildings and other kinds).</summary>
    public ActorLayer LayerOf(ActorKind kind, int i) => kind switch
    {
        ActorKind.Vehicle => VehicleLayer[i],
        ActorKind.Infantry => InfantryLayer[i],
        _ => ActorLayer.Surface,
    };

    /// <summary>Reset a mobile slot's tunnel data (spawn / removal).</summary>
    public void ClearMobile(ActorKind kind, int i)
    {
        if (kind == ActorKind.Vehicle) VehicleLayer[i] = ActorLayer.Surface;
        else if (kind == ActorKind.Infantry) { InfantryLayer[i] = ActorLayer.Surface; InfantryCarrier[i] = default; }
    }

    /// <summary>Reset a building slot's tunnel data (removal).</summary>
    public void ClearBuilding(int i) { BuildingMorphType[i] = 0; BuildingMorphTicks[i] = 0; }

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(VehicleLayer.Length); w.Write(InfantryLayer.Length); w.Write(BuildingMorphType.Length);
        w.Write(NextTicket);
        for (int i = 1; i < VehicleLayer.Length; i++) if (VehicleLayer[i] != 0) { w.Write(i); w.Write((byte)VehicleLayer[i]); }
        w.Write(0);
        for (int i = 1; i < InfantryLayer.Length; i++) if (InfantryLayer[i] != 0) { w.Write(i); w.Write((byte)InfantryLayer[i]); }
        w.Write(0);
        for (int i = 1; i < InfantryCarrier.Length; i++) if (!InfantryCarrier[i].IsNone) { w.Write(i); StateIo.WriteId(w, InfantryCarrier[i]); }
        w.Write(0);
        for (int i = 1; i < BuildingMorphType.Length; i++)
            if (BuildingMorphType[i] != 0 || BuildingMorphTicks[i] != 0) { w.Write(i); w.Write(BuildingMorphType[i]); w.Write(BuildingMorphTicks[i]); }
        w.Write(0);
        w.Write(Queue.Count);
        foreach (TunnelQueueEntry q in Queue)
        {
            StateIo.WriteId(w, q.Entrance); StateIo.WriteRef(w, q.Unit); StateIo.WriteId(w, q.Destination); w.Write(q.Ticket);
        }
        w.Write(Transit.Count);
        foreach (TunnelTransit t in Transit)
        {
            StateIo.WriteRef(w, t.Unit); w.Write(t.Owner); StateIo.WriteId(w, t.Destination); w.Write(t.TicksLeft); w.Write(t.Points);
        }
        w.Write(Telegraphs.Count);
        foreach (Telegraph g in Telegraphs)
        {
            StateIo.WriteCell(w, g.Cell); w.Write(g.Owner); w.Write(g.TicksLeft); w.Write((byte)g.Kind); StateIo.WriteRef(w, g.Subject);
        }
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        if (r.ReadByte() != Version) throw new InvalidDataException("tunnels version mismatch.");
        if (r.ReadInt32() != VehicleLayer.Length || r.ReadInt32() != InfantryLayer.Length || r.ReadInt32() != BuildingMorphType.Length)
            throw new InvalidDataException("tunnels capacity mismatch.");
        NextTicket = r.ReadInt32();
        Array.Clear(VehicleLayer); Array.Clear(InfantryLayer); Array.Clear(InfantryCarrier);
        Array.Clear(BuildingMorphType); Array.Clear(BuildingMorphTicks);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) VehicleLayer[StateIo.Slot(i, VehicleLayer.Length, Name)] = (ActorLayer)r.ReadByte();
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) InfantryLayer[StateIo.Slot(i, InfantryLayer.Length, Name)] = (ActorLayer)r.ReadByte();
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32()) InfantryCarrier[StateIo.Slot(i, InfantryCarrier.Length, Name)] = StateIo.ReadId(r);
        for (int i = r.ReadInt32(); i != 0; i = r.ReadInt32())
        {
            int s = StateIo.Slot(i, BuildingMorphType.Length, Name);
            BuildingMorphType[s] = r.ReadInt32(); BuildingMorphTicks[s] = r.ReadInt32();
        }
        Queue.Clear();
        for (int n = StateIo.Count(r, Name); n > 0; n--)
            Queue.Add(new TunnelQueueEntry { Entrance = StateIo.ReadId(r), Unit = StateIo.ReadRef(r), Destination = StateIo.ReadId(r), Ticket = r.ReadInt32() });
        Transit.Clear();
        for (int n = StateIo.Count(r, Name); n > 0; n--)
            Transit.Add(new TunnelTransit { Unit = StateIo.ReadRef(r), Owner = r.ReadByte(), Destination = StateIo.ReadId(r), TicksLeft = r.ReadInt32(), Points = r.ReadInt32() });
        Telegraphs.Clear();
        for (int n = StateIo.Count(r, Name); n > 0; n--)
            Telegraphs.Add(new Telegraph { Cell = StateIo.ReadCell(r), Owner = r.ReadByte(), TicksLeft = r.ReadInt32(), Kind = (TelegraphKind)r.ReadByte(), Subject = StateIo.ReadRef(r) });
    }
}
