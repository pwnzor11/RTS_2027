using Rts.Sim;
using Rts.Sim.Core;
using Rts.Sim.Replay;

namespace Rts.Game.Sandbox;

/// <summary>
/// Wraps <see cref="ReplayWriter"/> for one sandbox session: setup spawns, every command the session issues, and a
/// hash check every <see cref="SimConstants.HashIntervalTicks"/> ticks. Written once by <see cref="Save"/>.
/// </summary>
public sealed class ReplayRecording
{
    private readonly ReplayWriter _writer;
    private bool _saved;

    /// <summary>Starts recording for a sim built from <paramref name="setup"/>.</summary>
    public ReplayRecording(SimSetup setup)
    {
        _writer = new ReplayWriter(setup);
    }

    /// <summary>Records a setup spawn. Call in the same order as the spawns, before tick 0.</summary>
    public void RecordSpawn(ActorKind kind, byte player, int typeIndex, CellCoord cell, int subSlot = 0) =>
        _writer.RecordSpawn(kind, player, typeIndex, cell, subSlot);

    /// <summary>Records a command at the moment it is queued, so the replay carries exactly what the sim saw.</summary>
    public void RecordCommand(Command command) => _writer.RecordCommand(command);

    /// <summary>Records the state hash and sub-hashes at the sim's current tick.</summary>
    public void RecordHash(Simulation sim) => _writer.RecordHash(sim.Tick, sim.ComputeStateHash(), sim.ComputeSubHashes());

    /// <summary>
    /// Writes the replay to <paramref name="path"/> (folders are created). Returns false and prints the problem if the
    /// file cannot be written; the game keeps running either way. A second call does nothing.
    /// </summary>
    public bool Save(string path, int finalTick)
    {
        if (_saved) return true;
        try
        {
            string full = Path.GetFullPath(path);
            string? dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            using (var stream = File.Create(full)) _writer.Finish(stream, finalTick);
            _saved = true;
            Console.WriteLine($"replay saved: {full}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: replay not saved: {e.Message}");
            return false;
        }
    }
}
