// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;
using Rts.Sim.Serialization;

namespace Rts.Sim.Replay;

/// <summary>
/// One replay record. Offset is the byte position of the record's type byte in the stream. For a hash check,
/// HashTick and Hash are set and SubHashes holds the per-part sub-hashes when the writer recorded them (else null).
/// </summary>
public readonly record struct ReplayRecord(long Offset, byte Type, Command Command, int HashTick, ulong Hash,
    IReadOnlyList<(string Part, ulong Hash)>? SubHashes);

/// <summary>One setup spawn (Type indexes the vehicle, infantry or building list by Kind; SubSlot is infantry only).
/// Tick is the sim tick it happened at (0 for normal setup).</summary>
public readonly record struct ReplaySpawn(ActorKind Kind, byte Player, int Type, CellCoord Cell, int SubSlot, int Tick = 0);

/// <summary>Parsed replay file. Truncated is true when the end record is missing (the game crashed or was killed): every
/// complete record was read, and FinalTick is then the tick of the last hash record (0 if none).</summary>
public sealed record ReplayData(
    int Version, SetupHeader Header, IReadOnlyList<ReplaySpawn> Spawns,
    IReadOnlyList<ReplayRecord> Records, int FinalTick, bool Truncated)
{
    /// <summary>The scripted mission this replay was recorded on (id and script hash), or null (formats before 8, and every non-mission game).</summary>
    public ReplayMission? Mission { get; init; }

    public string Build => Header.Build;
    public ulong RulesHash => Header.RulesHash;
    public ulong MapHash => Header.MapHash;
    public ulong Seed => Header.Seed;
    public int MapWidth => Header.MapWidth;
    public int MapHeight => Header.MapHeight;
    public int PlayerCount => Header.PlayerCount;
    public IReadOnlyList<(string Name, string Text)> RulesFiles => Header.RulesFiles;
}

/// <summary>Parses replays. Throws InvalidDataException on bad magic, an unsupported version, a damaged header or a
/// malformed record. A file that simply ends (no end record, or a record cut short) is read as far as it goes.</summary>
public static class ReplayReader
{
    public static ReplayData Read(Stream input)
    {
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        SetupHeader header;
        ReplayMission? mission = null;
        int version;
        try
        {
            if (!r.ReadBytes(4).AsSpan().SequenceEqual(ReplayWriter.Magic)) throw new InvalidDataException("Bad replay magic.");
            version = r.ReadInt32();
            if (version < ReplayWriter.OldestReadableVersion || version > ReplayWriter.Version)
                throw new InvalidDataException($"Unsupported replay format version {version}; this build reads version {ReplayWriter.OldestReadableVersion} or {ReplayWriter.Version}. " +
                    "Replays from other versions cannot be played back; play it with the build that recorded it.");
            header = SetupHeader.Read(r);
            if (version >= 8) mission = ReplayMission.Read(r);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Replay header is truncated.");
        }

        var spawns = new List<ReplaySpawn>();
        var records = new List<ReplayRecord>();
        int lastHashTick = 0;
        bool truncated = false;
        int finalTick = 0;
        while (true)
        {
            long off = r.BaseStream.Position;
            int first = r.BaseStream.ReadByte();
            if (first < 0) { truncated = true; break; }
            byte type = (byte)first;
            try
            {
                if (type == ReplayWriter.TypeEnd)
                {
                    finalTick = r.ReadInt32();
                    break;
                }
                if (type == ReplayWriter.TypeSpawn)
                {
                    int tick = r.ReadInt32();
                    var kind = (ActorKind)r.ReadByte();
                    byte player = r.ReadByte();
                    int typeIndex = r.ReadInt32();
                    var cell = new CellCoord(r.ReadInt32(), r.ReadInt32());
                    spawns.Add(new ReplaySpawn(kind, player, typeIndex, cell, r.ReadByte(), tick));
                }
                else if (type == ReplayWriter.TypeCommand)
                {
                    int tick = r.ReadInt32(); byte player = r.ReadByte(); ushort seq = r.ReadUInt16(); var kind = (CommandKind)r.ReadByte();
                    var subj = new EntityId(r.ReadUInt16(), r.ReadUInt16());
                    var targ = new EntityId(r.ReadUInt16(), r.ReadUInt16());
                    var cell = new CellCoord(r.ReadInt32(), r.ReadInt32());
                    int arg = r.ReadInt32();
                    int arg2 = r.ReadInt32();
                    records.Add(new ReplayRecord(off, type, new Command(tick, player, seq, kind, subj, targ, cell, arg, arg2), 0, 0, null));
                }
                else if (type == ReplayWriter.TypeHashCheck)
                {
                    int hashTick = r.ReadInt32();
                    ulong hash = r.ReadUInt64();
                    byte flag = r.ReadByte();
                    IReadOnlyList<(string Part, ulong Hash)>? sub = null;
                    if (flag == 1)
                    {
                        int count = r.ReadInt32();
                        if (count < 0 || count > 1024) throw new InvalidDataException("Bad sub-hash count.");
                        var list = new List<(string Part, ulong Hash)>(count);
                        for (int k = 0; k < count; k++)
                        {
                            string part = r.ReadString();
                            list.Add((part, r.ReadUInt64()));
                        }
                        sub = list;
                    }
                    else if (flag != 0) throw new InvalidDataException($"Bad sub-hash flag {flag}.");
                    records.Add(new ReplayRecord(off, type, default, hashTick, hash, sub));
                    lastHashTick = hashTick;
                }
                else throw new InvalidDataException($"Unknown replay record type {type}.");
            }
            catch (EndOfStreamException)
            {
                truncated = true;   // the last record was cut short: drop it
                break;
            }
        }
        return new ReplayData(version, header, spawns, records, truncated ? lastHashTick : finalTick, truncated) { Mission = mission };
    }
}
