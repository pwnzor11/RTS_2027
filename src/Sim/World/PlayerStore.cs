// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Rules;

namespace Rts.Sim.World;

/// <summary>
/// Per-player sim state, the "players" state part (M1.5, D-054, review B-3). One entry per player slot.
/// Team comes from the setup and never changes. Cash/Stored follow design 02 §2 (Cash uncapped, Stored capped
/// by storage; both spendable). PowerOut/PowerUse are recomputed by the economy (M2). Defeated is set by the
/// win-condition rule (M2). Mutate only inside Step (or via setup-time scenario code).
/// M3-01: <see cref="Faction"/> comes from the setup (<see cref="SimSetup.FactionOf"/>) and never changes; <see cref="Upgrades"/>
/// is the per-player upgrade mask (bit = index in SimRules.Upgrades), set when research finishes (M3-06). Both are written after
/// the M2 fields (appended at the end of the part).
/// </summary>
public sealed class PlayerStore : ISimStatePart
{
    public PlayerStore(SimSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);
        Count = setup.PlayerCount;
        Team = new byte[Count];
        Cash = new int[Count];
        Stored = new int[Count];
        PowerOut = new int[Count];
        PowerUse = new int[Count];
        Defeated = new bool[Count];
        Faction = new Faction[Count];
        Upgrades = new ulong[Count];
        for (int p = 0; p < Count; p++) { Team[p] = setup.TeamOf((byte)p); Faction[p] = setup.FactionOf((byte)p); }
    }

    public string Name => "players";
    public int Count { get; }
    public readonly byte[] Team;
    public readonly int[] Cash;
    public readonly int[] Stored;
    public readonly int[] PowerOut;
    public readonly int[] PowerUse;
    public readonly bool[] Defeated;
    /// <summary>Faction per player (from the setup; constant).</summary>
    public readonly Faction[] Faction;
    /// <summary>Upgrade mask per player: bit i = SimRules.Upgrades[i] researched.</summary>
    public readonly ulong[] Upgrades;

    /// <summary>True if the player has researched upgrade <paramref name="upgradeIndex"/> (false for a bad player or index).</summary>
    public bool HasUpgrade(byte player, int upgradeIndex) =>
        player < Count && upgradeIndex >= 0 && upgradeIndex < RuleCounts.MaxUpgrades && (Upgrades[player] & (1UL << upgradeIndex)) != 0;

    /// <summary>True if the slot is neutral (team <see cref="Teams.Neutral"/>).</summary>
    public bool IsNeutral(byte player) => player < Count && Team[player] == Teams.Neutral;

    public void Write(BinaryWriter w)
    {
        w.Write(Count);
        for (int p = 0; p < Count; p++)
        {
            w.Write(Team[p]); w.Write(Cash[p]); w.Write(Stored[p]);
            w.Write(PowerOut[p]); w.Write(PowerUse[p]); w.Write(Defeated[p]);
        }
        for (int p = 0; p < Count; p++) { w.Write((byte)Faction[p]); w.Write(Upgrades[p]); }   // M3-01, appended
    }

    public void Read(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n != Count) throw new InvalidDataException("players count mismatch.");
        for (int p = 0; p < Count; p++)
        {
            byte team = r.ReadByte();
            if (team != Team[p]) throw new InvalidDataException("players team mismatch.");
            Cash[p] = r.ReadInt32(); Stored[p] = r.ReadInt32();
            PowerOut[p] = r.ReadInt32(); PowerUse[p] = r.ReadInt32(); Defeated[p] = r.ReadBoolean();
        }
        for (int p = 0; p < Count; p++)
        {
            if ((Faction)r.ReadByte() != Faction[p]) throw new InvalidDataException("players faction mismatch.");
            Upgrades[p] = r.ReadUInt64();
        }
    }
}
