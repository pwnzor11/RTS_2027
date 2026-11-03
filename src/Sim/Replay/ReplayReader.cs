using Rts.Sim.Core;

namespace Rts.Sim.Replay;

/// <summary>One replay record. Offset is the byte position of the record's type byte in the stream.</summary>
public readonly record struct ReplayRecord(long Offset, byte Type, Command Command, int HashTick, ulong Hash);

/// <summary>Parsed replay file.</summary>
public sealed record ReplayData(
    ulong RulesHash, ulong MapHash, ulong Seed, int MapWidth, int MapHeight, int PlayerCount,
    IReadOnlyList<(byte Player, int Type, CellCoord Cell)> Spawns,
    IReadOnlyList<ReplayRecord> Records, int FinalTick);

/// <summary>Parses replays. Throws InvalidDataException on bad magic or version.</summary>
public static class ReplayReader
{
    public static ReplayData Read(Stream input)
    {
        using var r = new BinaryReader(input, System.Text.Encoding.UTF8, leaveOpen: true);
        if (!r.ReadBytes(4).AsSpan().SequenceEqual(ReplayWriter.Magic)) throw new InvalidDataException("Bad replay magic.");
        int version = r.ReadInt32();
        if (version != ReplayWriter.Version) throw new InvalidDataException($"Unsupported replay version {version}.");
        ulong rules = r.ReadUInt64(), mapHash = r.ReadUInt64(), seed = r.ReadUInt64();
        int w = r.ReadInt32(), h = r.ReadInt32(), players = r.ReadInt32();
        int spawnCount = r.ReadInt32();
        var spawns = new List<(byte, int, CellCoord)>(spawnCount);
        for (int i = 0; i < spawnCount; i++)
            spawns.Add((r.ReadByte(), r.ReadInt32(), new CellCoord(r.ReadInt32(), r.ReadInt32())));
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
                records.Add(new ReplayRecord(off, type, new Command(tick, player, seq, kind, subj, targ, cell, arg), 0, 0));
            }
            else if (type == ReplayWriter.TypeHashCheck)
                records.Add(new ReplayRecord(off, type, default, r.ReadInt32(), r.ReadUInt64()));
            else throw new InvalidDataException($"Unknown replay record type {type}.");
        }
    }
}
