using Rts.Sim.Core;

namespace Rts.Sim.World;

/// <summary>
/// Per-player sim state, the "players" state part (M1.5, D-054, review B-3). One entry per player slot.
/// Team comes from the setup and never changes. Cash/Stored follow design 02 §2 (Cash uncapped, Stored capped
/// by storage; both spendable). PowerOut/PowerUse are recomputed by the economy (M2). Defeated is set by the
/// win-condition rule (M2). Mutate only inside Step (or via setup-time scenario code).
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
        for (int p = 0; p < Count; p++) Team[p] = setup.TeamOf((byte)p);
    }

    public string Name => "players";
    public int Count { get; }
    public readonly byte[] Team;
    public readonly int[] Cash;
    public readonly int[] Stored;
    public readonly int[] PowerOut;
    public readonly int[] PowerUse;
    public readonly bool[] Defeated;

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
    }
}
