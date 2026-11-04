// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;

namespace Rts.Data;

/// <summary>
/// The rules source files as (relative name, text) pairs: what replays and saves embed (M1.5, review A-2) and how
/// they become rules again without the original data/rules directory.
/// </summary>
public static class RulesBundle
{
    /// <summary>The files <see cref="RulesLoader"/> reads, in a fixed order.</summary>
    public static readonly IReadOnlyList<string> FileNames =
    [
        RulesLoader.GameFile, RulesLoader.WarheadsFile, RulesLoader.WeaponsFile, RulesLoader.VehiclesFile,
        RulesLoader.InfantryFile, RulesLoader.BuildingsFile, RulesLoader.TerrainFile, RulesLoader.TechsFile,
        RulesLoader.UpgradesFile,   // M3, optional
    ];

    /// <summary>Reads the rules files from a directory. A missing file is left out (loading then reports it).</summary>
    public static IReadOnlyList<(string Name, string Text)> ReadDirectory(string rulesDir)
    {
        var files = new List<(string Name, string Text)>(FileNames.Count);
        foreach (string name in FileNames)
        {
            string path = Path.Combine(rulesDir, name);
            if (File.Exists(path)) files.Add((name, File.ReadAllText(path)));
        }
        return files;
    }

    /// <summary>Loads rules from in-memory files.</summary>
    /// <exception cref="RulesException">A file is missing, unparseable or invalid.</exception>
    public static RuleSet Load(IReadOnlyList<(string Name, string Text)> files)
    {
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string name, string text) in files) byName[name] = text;
        return RulesLoader.Load(name => byName.TryGetValue(name, out string? text) ? text : null);
    }

    /// <summary>The delegate shape <c>Simulation.Load</c> wants: files in, rules and rules hash out.</summary>
    public static (SimRules Rules, ulong Hash) ToSimRules(IReadOnlyList<(string Name, string Text)> files)
    {
        RuleSet set = Load(files);
        return (set.Sim, set.RulesHash);
    }
}
