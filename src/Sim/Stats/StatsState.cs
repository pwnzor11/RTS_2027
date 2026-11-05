// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Sim.Stats;

/// <summary>
/// The "stats" state part (M4-01 skeleton; M4-06 fills the counters through the hooks in Simulation.Stats.cs). Appended after
/// "garrison". Per player slot, all zero at the start: units built and lost, structures built and lost, kills (credited to the
/// owner of the lethal hit), credits harvested and credits spent. Deterministic and saved, so the spectator stats panel and the
/// game-over table read the same numbers in a live game, a loaded save and a replay.
/// Layout: version, player count, then the seven arrays in declaration order, each written in full.
/// </summary>
public sealed class StatsState : ISimStatePart
{
    private const byte Version = 1;
    private const byte VersionWithShipments = 2;

    /// <summary>Vehicles and infantry produced or spawned, per player.</summary>
    public readonly int[] UnitsBuilt;
    /// <summary>Vehicles and infantry lost, per player.</summary>
    public readonly int[] UnitsLost;
    /// <summary>Buildings placed, per player.</summary>
    public readonly int[] StructuresBuilt;
    /// <summary>Buildings lost, per player.</summary>
    public readonly int[] StructuresLost;
    /// <summary>Enemy actors killed, per player.</summary>
    public readonly int[] Kills;
    /// <summary>Credits brought in by harvesters, per player.</summary>
    public readonly int[] CreditsHarvested;
    /// <summary>Credits spent, per player.</summary>
    public readonly int[] CreditsSpent;

    /// <summary>M7-06: credits delivered by home shipments (tenders that reached their dock), per player. Appended; serialised (part version 2)
    /// only once any player has a non-zero value, so a game without shipments hashes as before.</summary>
    public readonly int[] ShipmentIncome;

    /// <summary>Creates an all-zero part sized by the setup's player count.</summary>
    public StatsState(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        int n = setup.PlayerCount;
        UnitsBuilt = new int[n]; UnitsLost = new int[n]; StructuresBuilt = new int[n]; StructuresLost = new int[n];
        ShipmentIncome = new int[n];
        Kills = new int[n]; CreditsHarvested = new int[n]; CreditsSpent = new int[n];
    }

    /// <inheritdoc />
    public string Name => "stats";

    /// <summary>Number of player slots.</summary>
    public int Count => UnitsBuilt.Length;

    /// <summary>The counters of one player as a value (zeros for an out-of-range slot).</summary>
    public PlayerStats Of(byte player) => player >= Count ? default
        : new PlayerStats(UnitsBuilt[player], UnitsLost[player], StructuresBuilt[player], StructuresLost[player], Kills[player],
            CreditsHarvested[player], CreditsSpent[player], ShipmentIncome[player]);

    private int[][] All => new[] { UnitsBuilt, UnitsLost, StructuresBuilt, StructuresLost, Kills, CreditsHarvested, CreditsSpent };

    /// <inheritdoc />
    public void Write(BinaryWriter w)
    {
        bool ship = Array.Exists(ShipmentIncome, x => x != 0);
        w.Write(ship ? VersionWithShipments : Version);
        w.Write(Count);
        foreach (int[] a in All) for (int p = 0; p < a.Length; p++) w.Write(a[p]);
        if (ship) for (int p = 0; p < ShipmentIncome.Length; p++) w.Write(ShipmentIncome[p]);
    }

    /// <inheritdoc />
    public void Read(BinaryReader r)
    {
        byte version = r.ReadByte();
        if (version != Version && version != VersionWithShipments) throw new InvalidDataException("stats version mismatch.");
        if (r.ReadInt32() != Count) throw new InvalidDataException("stats player count mismatch.");
        foreach (int[] a in All) for (int p = 0; p < a.Length; p++) a[p] = r.ReadInt32();
        for (int p = 0; p < ShipmentIncome.Length; p++) ShipmentIncome[p] = version == VersionWithShipments ? r.ReadInt32() : 0;
    }
}
