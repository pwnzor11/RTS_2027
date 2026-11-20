// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;

namespace Rts.Scripting.Tests.M11_30;

/// <summary>
/// M11-30: every mission objective has a location for the HUD (<c>Objective.SetTarget</c>), or is listed in <see cref="LocationFree"/> with a
/// reason. Checked by reading the mission scripts: each <c>Objective.Add</c> or <c>AddHidden</c> with a literal id must have a
/// <c>SetTarget</c> call with that id somewhere in the same script.
/// </summary>
public sealed class MissionObjectiveLocationTests
{
    /// <summary>
    /// Objectives that are genuinely location-free, as "mission/objective" to the reason. Empty: every objective in the campaign is about a
    /// zone, a cell or a unit that has a place. Add an entry only with a reason, and only for a goal that has no place at all.
    /// </summary>
    private static readonly Dictionary<string, string> LocationFree = new();

    private static readonly Regex AddCall = new(@"Objective\.(?:Add|AddHidden)\(\s*(""([^""]*)""|[^\s""])");
    private static readonly Regex SetTargetCall = new(@"Objective\.SetTarget\(\s*""([^""]*)""");

    /// <summary>The script with its "--" comments removed, so a commented-out call does not count.</summary>
    private static string Code(string lua) =>
        string.Join("\n", lua.Split('\n').Select(line =>
        {
            int c = line.IndexOf("--", StringComparison.Ordinal);
            return c >= 0 ? line[..c] : line;
        }));

    private static string MissionsDir => Path.Combine(TestData.DataDir, "missions");

    [Fact]
    public void Every_mission_loads_without_script_errors()
    {
        var failures = new List<string>();
        foreach (string folder in Directory.GetDirectories(MissionsDir).OrderBy(static d => d, StringComparer.Ordinal))
        {
            string id = Path.GetFileName(folder);
            MissionSession m = MissionSession.Create(TestData.DataDir, id);
            TestData.Run(m.Sim, 2);
            foreach (string e in m.Host.Errors) failures.Add(id + ": " + e);
        }

        Assert.Empty(failures);
    }

    [Fact]
    public void Every_objective_has_a_target_or_an_allow_listed_reason()
    {
        var problems = new List<string>();
        var seenFiles = 0;
        foreach (string folder in Directory.GetDirectories(MissionsDir).OrderBy(static d => d, StringComparer.Ordinal))
        {
            string id = Path.GetFileName(folder);
            string file = Path.Combine(folder, "mission.lua");
            if (!File.Exists(file)) continue;
            seenFiles++;
            string code = Code(File.ReadAllText(file));

            var added = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in AddCall.Matches(code))
            {
                if (!m.Groups[2].Success) { problems.Add(id + ": Objective.Add with a non-literal id (the check cannot read it)"); continue; }
                added.Add(m.Groups[2].Value);
            }

            var targeted = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in SetTargetCall.Matches(code)) targeted.Add(m.Groups[1].Value);

            foreach (string objective in added.OrderBy(static s => s, StringComparer.Ordinal))
            {
                string key = id + "/" + objective;
                if (targeted.Contains(objective))
                {
                    if (LocationFree.ContainsKey(key)) problems.Add(key + ": on the allow-list but has a SetTarget; remove it from the list");
                    continue;
                }

                if (!LocationFree.ContainsKey(key)) problems.Add(key + ": no Objective.SetTarget call (give it a zone, cell or actor tag)");
            }

            foreach (string t in targeted.Where(t => !added.Contains(t)).OrderBy(static s => s, StringComparer.Ordinal))
                problems.Add(id + "/" + t + ": Objective.SetTarget for an objective that is never added");
        }

        Assert.True(seenFiles > 0, "no mission scripts found");
        Assert.Empty(problems);
    }
}
