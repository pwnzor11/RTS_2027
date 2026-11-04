// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Sim.Core;
using Rts.Sim.Map;

namespace Rts.Game.Tests.Skirmish;

/// <summary>A temp folder and a flat map with four starts, so 3 and 4 player games do not depend on the procedural generator.</summary>
internal sealed class SkirmishTestMaps : IDisposable
{
    public SkirmishTestMaps()
    {
        Folder = Path.Combine(Path.GetTempPath(), "rts2027_skirmish_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Folder);
        RulesDir = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
    }

    public string Folder { get; }
    public string RulesDir { get; }

    /// <summary>Writes a 40x40 all-clear map with starts in the four corners (top-left, top-right, bottom-right, bottom-left) and returns its path.</summary>
    public string WriteFourStartMap(string name = "four_start", int maxPlayers = 4, IReadOnlyList<MapFeature>? features = null)
    {
        const int n = 40;
        var starts = new[] { new CellCoord(6, 6), new CellCoord(n - 7, 6), new CellCoord(n - 7, n - 7), new CellCoord(6, n - 7) };
        var map = MapData.CreateFlat(n, n, name);
        map = new MapData(name, map.Theater, n, n, new ushort[n * n], map.Land.ToArray(), new byte[n * n], map.Overlay.ToArray(), new byte[n * n], starts);
        string path = Path.Combine(Folder, name + ".map.toml");
        MapFile.Save(map, new MapMeta("", MapSymmetry.None, maxPlayers, null, features ?? Array.Empty<MapFeature>()), path);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(Folder, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
