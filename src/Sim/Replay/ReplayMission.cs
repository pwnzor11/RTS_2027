// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Replay;

/// <summary>
/// The mission a replay was recorded on (format 8, M3-19): the mission id (folder name under data/missions) and the hash of its
/// script source. The Sim only stores them; the scripting layer re-creates the mission and checks the hash before playing.
/// </summary>
public sealed record ReplayMission(string Id, ulong ScriptHash)
{
    internal static void Write(BinaryWriter w, ReplayMission? mission)
    {
        if (mission is null) { w.Write((byte)0); return; }
        w.Write((byte)1);
        w.Write(mission.Id);
        w.Write(mission.ScriptHash);
    }

    internal static ReplayMission? Read(BinaryReader r)
    {
        byte flag = r.ReadByte();
        if (flag == 0) return null;
        if (flag != 1) throw new InvalidDataException($"Bad mission flag {flag}.");
        string id = r.ReadString();
        if (id.Length == 0 || id.Length > 256) throw new InvalidDataException("Bad mission id.");
        return new ReplayMission(id, r.ReadUInt64());
    }
}
