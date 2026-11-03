using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Sim.Replay;

/// <summary>
/// Records a game. Header + spawn list + records (Command / HashCheck) + end marker. Records are buffered
/// and the whole file is written by Finish.
/// </summary>
public sealed class ReplayWriter
{
    public static readonly byte[] Magic = "RTSR"u8.ToArray();
    public const int Version = 2;
    public const byte TypeEnd = 0, TypeCommand = 1, TypeHashCheck = 2;

    private readonly SimSetup _setup;
    private readonly ulong _mapHash;
    private readonly List<(byte Player, int Type, CellCoord Cell)> _spawns = new();
    private readonly MemoryStream _records = new();
    private readonly BinaryWriter _rw;

    public ReplayWriter(SimSetup setup)
    {
        _setup = setup;
        _mapHash = MapHasher.Compute(setup.Map);
        _rw = new BinaryWriter(_records);
    }

    /// <summary>Record an initial spawn (must be called before tick 0, in the same order as the Simulation spawns).</summary>
    public void RecordSpawn(byte player, int vehicleTypeIndex, CellCoord cell) => _spawns.Add((player, vehicleTypeIndex, cell));

    public void RecordCommand(Command c)
    {
        _rw.Write(TypeCommand);
        _rw.Write(c.ExecTick); _rw.Write(c.Player); _rw.Write(c.Sequence); _rw.Write((byte)c.Kind);
        _rw.Write(c.Subject.Index); _rw.Write(c.Subject.Generation);
        _rw.Write(c.Target.Index); _rw.Write(c.Target.Generation);
        _rw.Write(c.Cell.X); _rw.Write(c.Cell.Y); _rw.Write(c.Arg);
    }

    public void RecordHash(int tick, ulong hash)
    {
        _rw.Write(TypeHashCheck); _rw.Write(tick); _rw.Write(hash);
    }

    /// <summary>Write the complete replay. finalTick is the sim tick when recording stopped.</summary>
    public void Finish(Stream output, int finalTick)
    {
        using var w = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true);
        w.Write(Magic); w.Write(Version);
        w.Write(_setup.RulesHash); w.Write(_mapHash); w.Write(_setup.Seed);
        w.Write(_setup.Map.Width); w.Write(_setup.Map.Height); w.Write(_setup.PlayerCount);
        w.Write(_spawns.Count);
        foreach (var s in _spawns) { w.Write(s.Player); w.Write(s.Type); w.Write(s.Cell.X); w.Write(s.Cell.Y); }
        _rw.Flush();
        w.Write(_records.GetBuffer(), 0, (int)_records.Length);
        w.Write(TypeEnd); w.Write(finalTick);
    }
}
