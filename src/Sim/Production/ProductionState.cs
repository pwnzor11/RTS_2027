// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.Production;

/// <summary>
/// The "production" state part (M2-08). Everything is flat arrays indexed by <c>player * Categories + category</c>
/// (a "lane") or by building slot. Appended after "players" (append-only order).
/// Per lane: the queue (type indices, front first), the front item's progress (credits spent so far, ticks done, ticks
/// total at the last tick), a stalled flag (the last tick could not pay), the finished structure waiting to be placed
/// (<see cref="Ready"/>, -1 = none), the primary factory and the rally cell. Per building slot: the repair switch
/// (stored as slot generation + 1 so a reused slot never inherits it) and the repair credit remainder. Per player: whether
/// the player ever owned a non-wall building (the win rule only defeats players who once had a base).
/// </summary>
public sealed class ProductionState : ISimStatePart
{
    private const byte Version = 3;   // 3: six lanes per player (M3-01 added ProductionCategory.Upgrade)

    /// <summary>Number of <see cref="ProductionCategory"/> values. The Upgrade lane (5) exists from M3-01 and is driven by M3-06;
    /// the M2 production loop and HUD queue views still cover Infantry..Defence only.</summary>
    public const int Categories = RuleCounts.ProductionCategories;

    private readonly int _players;
    private readonly int _maxQueue;
    private readonly int _buildingCapacity;

    internal readonly int[] QueueType;      // [lane * maxQueue + position]
    internal readonly int[] QueueCount;     // [lane]
    internal readonly int[] Spent;          // [lane] credits already paid for the front item
    internal readonly int[] Ticks;          // [lane] progress ticks of the front item (against Total)
    internal readonly int[] Total;          // [lane] total ticks at the last tick (0 = not started)
    internal readonly bool[] Stalled;       // [lane]
    internal readonly int[] Ready;          // [lane] building type index waiting to be placed, or -1
    internal readonly EntityId[] Primary;   // [lane]
    internal readonly bool[] RallySet;      // [lane]
    internal readonly CellCoord[] Rally;    // [lane]
    internal readonly int[] RepairGen;      // [building slot] generation + 1 while repairing, else 0
    internal readonly int[] RepairFrac;     // [building slot] remainder of the repair cost, in 1/(MaxHealth*1000) credits
    internal readonly bool[] EverHadBase;   // [player]

    /// <summary>Creates empty production state sized from the setup (player count, queue length, building capacity).</summary>
    public ProductionState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        _players = setup.PlayerCount;
        _maxQueue = Math.Max(1, setup.Rules?.Economy.MaxQueue ?? 1);
        _buildingCapacity = setup.Limits.Buildings;
        int lanes = _players * Categories;
        QueueType = new int[lanes * _maxQueue];
        QueueCount = new int[lanes];
        Spent = new int[lanes]; Ticks = new int[lanes]; Total = new int[lanes];
        Stalled = new bool[lanes];
        Ready = new int[lanes];
        Array.Fill(Ready, -1);
        Primary = new EntityId[lanes];
        RallySet = new bool[lanes];
        Rally = new CellCoord[lanes];
        RepairGen = new int[_buildingCapacity];
        RepairFrac = new int[_buildingCapacity];
        EverHadBase = new bool[_players];
    }

    /// <inheritdoc />
    public string Name => "production";

    /// <summary>Index of a player's lane for a category.</summary>
    internal static int Lane(int player, ProductionCategory category) => player * Categories + (int)category;

    /// <summary>Longest queue (rules MaxQueue).</summary>
    internal int MaxQueue => _maxQueue;

    /// <summary>Items queued for the player in the category.</summary>
    public int QueueLength(byte player, ProductionCategory category) => QueueCount[Lane(player, category)];

    /// <summary>Type index at a queue position (0 = front), or -1.</summary>
    public int QueuedType(byte player, ProductionCategory category, int position)
    {
        int lane = Lane(player, category);
        return position >= 0 && position < QueueCount[lane] ? QueueType[lane * _maxQueue + position] : -1;
    }

    /// <summary>Finished structure/defence type index waiting to be placed, or -1.</summary>
    public int ReadyType(byte player, ProductionCategory category) => Ready[Lane(player, category)];

    /// <summary>Credits already paid for the front item of the queue.</summary>
    public int CreditsSpent(byte player, ProductionCategory category) => Spent[Lane(player, category)];

    /// <summary>True if the front item could not pay on the last tick (or could not find a free exit cell).</summary>
    public bool IsStalled(byte player, ProductionCategory category) => Stalled[Lane(player, category)];

    /// <summary>The factory explicitly chosen by a Rally command (may be dead; the sim then falls back), else none.</summary>
    public EntityId PrimaryFactory(byte player, ProductionCategory category) => Primary[Lane(player, category)];

    /// <summary>The rally cell of the lane, if one was set.</summary>
    public CellCoord? RallyCell(byte player, ProductionCategory category)
    {
        int lane = Lane(player, category);
        return RallySet[lane] ? Rally[lane] : null;
    }

    /// <summary>Front-item progress, 0..1000.</summary>
    internal int ProgressPermille(int lane) => Total[lane] > 0 ? (int)((long)Ticks[lane] * 1000 / Total[lane]) : 0;

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        w.Write(Version);
        w.Write(_players); w.Write(_maxQueue); w.Write(_buildingCapacity);
        for (int i = 0; i < QueueType.Length; i++) w.Write(QueueType[i]);
        for (int l = 0; l < QueueCount.Length; l++)
        {
            w.Write(QueueCount[l]); w.Write(Spent[l]); w.Write(Ticks[l]); w.Write(Total[l]); w.Write(Stalled[l]);
            w.Write(Ready[l]); w.Write(Primary[l].Index); w.Write(Primary[l].Generation);
            w.Write(RallySet[l]); w.Write(Rally[l].X); w.Write(Rally[l].Y);
        }
        for (int b = 0; b < _buildingCapacity; b++) { w.Write(RepairGen[b]); w.Write(RepairFrac[b]); }
        for (int p = 0; p < _players; p++) w.Write(EverHadBase[p]);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version != Version) throw new InvalidDataException("production version mismatch.");
        if (r.ReadInt32() != _players || r.ReadInt32() != _maxQueue || r.ReadInt32() != _buildingCapacity)
            throw new InvalidDataException("production size mismatch.");
        for (int i = 0; i < QueueType.Length; i++) QueueType[i] = r.ReadInt32();
        for (int l = 0; l < QueueCount.Length; l++)
        {
            QueueCount[l] = r.ReadInt32(); Spent[l] = r.ReadInt32(); Ticks[l] = r.ReadInt32(); Total[l] = r.ReadInt32();
            Stalled[l] = r.ReadBoolean(); Ready[l] = r.ReadInt32();
            ushort index = r.ReadUInt16(), gen = r.ReadUInt16();
            Primary[l] = new EntityId(index, gen);
            RallySet[l] = r.ReadBoolean(); Rally[l] = new CellCoord(r.ReadInt32(), r.ReadInt32());
            if (QueueCount[l] < 0 || QueueCount[l] > _maxQueue) throw new InvalidDataException("production queue length.");
        }
        for (int b = 0; b < _buildingCapacity; b++) { RepairGen[b] = r.ReadInt32(); RepairFrac[b] = r.ReadInt32(); }
        for (int p = 0; p < _players; p++) EverHadBase[p] = r.ReadBoolean();
    }
}
