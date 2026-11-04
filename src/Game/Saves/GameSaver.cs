// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Menu;

namespace Rts.Game.Saves;

/// <summary>Writes a running game to a save file.</summary>
public static class GameSaver
{
    /// <summary>
    /// The header to write now: the game's fixed <paramref name="template"/> (mode, map, rules, AI slots, mission) completed with the
    /// current tick and the commands still queued in <paramref name="host"/>.
    /// </summary>
    public static GameSaveHeader Capture(GameSaveHeader template, SimHost host) =>
        template with
        {
            Version = GameSaveHeader.CurrentVersion,
            Tick = host.Tick,
            Build = BuildInfo.Version,
            Pending = host.Pending.ToArray(),
            Sequences = host.Sequences,
            Ai = template.Ai.Select(a => a with { State = host.Ai?.CaptureState(a.Slot) }).ToArray(),   // M3-21: each AI's own memory
        };

    /// <summary>
    /// Saves into slot <paramref name="slotName"/> of the catalog. The file is written to a temporary name and moved over the slot, so a
    /// failed write never destroys an older save. Returns the path.
    /// </summary>
    /// <exception cref="IOException">The folder or file could not be written.</exception>
    public static string Save(SaveCatalog catalog, GameSaveHeader template, SimHost host, string rulesDir, string slotName)
    {
        string path = SaveSlots.PathFor(catalog, SaveSlots.KindOf(template.Mode), slotName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        try
        {
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                GameSaveFile.Write(file, Capture(template, host), host.Sim, RulesBundle.ReadDirectory(rulesDir));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
        return path;
    }
}
