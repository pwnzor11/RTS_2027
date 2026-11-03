using Rts.Sim.Core;

namespace Rts.Sim.Replay;

/// <summary>
/// One replay record. Offset is the byte position of the record's type byte in the stream. For a hash check,
/// HashTick and Hash are set and SubHashes holds the per-part sub-hashes when the writer recorded them (else null).
/// </summary>
public readonly record struct ReplayRecord(long Offset, byte Type, Command Command, int HashTick, ulong Hash,
    IReadOnlyList<(string Part, ulong Hash)>? SubHashes);

/// <summary>One initial spawn (Type indexes the vehicle or infantry list by Kind; SubSlot is infantry only).</summary>
public readonly record struct ReplaySpawn(ActorKind Kind, byte Player, int Type, CellCoord Cell, int SubSlot);

/// <summary>Parsed replay file.</summary>
public sealed record ReplayData(
    ulong RulesHash, ulong MapHash, ulong Seed, int MapWidth, int MapHeight, int PlayerCount,
    IReadOnlyList<ReplaySpawn> Spawns,
    IReadOnlyList<ReplayRecord> Records, int FinalTick);

/// <summary>Parses replays. Throws InvalidDataException on bad magic, an unsupported version or a malformed record.</summary>
public static class ReplayReader
{
    public static ReplayData Read(Stream input)
    {
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        if (!r.ReadBytes(4).AsSpan().SequenceEqual(ReplayWriter.Magic)) throw new InvalidDataException("Bad replay magic.");
        int version = r.ReadInt32();
        if (version != ReplayWriter.Version) throw new InvalidDataException($"Unsupported replay version {version}; expected {ReplayWriter.Version}.");
        ulong rules = r.ReadUInt64(), mapHash = r.ReadUInt64(), seed = r.ReadUInt64();
        int w = r.ReadInt32(), h = r.ReadInt32(), players = r.ReadInt32();
        int spawnCount = r.ReadInt32();
        var spawns = new List<ReplaySpawn>(spawnCount);
        for (int i = 0; i < spawnCount; i++)
            spawns.Add(new ReplaySpawn((ActorKind)r.ReadByte(), r.ReadByte(), r.ReadInt32(),
                new CellCoord(r.ReadInt32(), r.ReadInt32()), r.ReadByte()));
        var records = new List<ReplayRecord>();
        while (true)
        {
            long off = r.BaseStream.Position;
            byte type = r.ReadByte();
            if (type == ReplayWriter.TypeEnd)
                return new ReplayData(rules, mapHash, seed, w, h, players, spawns, records, r.ReadInt32());
            if (type == ReplayWriter.TypeCommand)
            {
                int tick = r.ReadInt32(); byte player = r.ReadByte(); ushort seq = r.ReadUInt16(); var kind = (CommandKind)r.ReadByte();
                var subj = new EntityId(r.ReadUInt16(), r.ReadUInt16());
                var targ = new EntityId(r.ReadUInt16(), r.ReadUInt16());
                var cell = new CellCoord(r.ReadInt32(), r.ReadInt32());
                int arg = r.ReadInt32();
                records.Add(new ReplayRecord(off, type, new Command(tick, player, seq, kind, subj, targ, cell, arg), 0, 0, null));
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
                    if (count < 0) throw new InvalidDataException("Negative sub-hash count.");
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
            }
            else throw new InvalidDataException($"Unknown replay record type {type}.");
        }
    }
}
