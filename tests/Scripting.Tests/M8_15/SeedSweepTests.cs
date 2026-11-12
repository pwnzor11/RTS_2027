// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace Rts.Scripting.Tests.M8_15;

/// <summary>
/// M13-17 (D-218, Q-75): the campaign retune measures each mission-tier as bot wins over 12 mission seeds (seed = 1000 + k * 7919,
/// k = 1..12), the session 20 probe made a kept tool. It does nothing unless <c>RTS_SWEEP</c> names the cases, so the normal suite
/// skips it in no time. Run:
/// <c>RTS_SWEEP="auth_01:3,lb_03:5" dotnet test tests/Scripting.Tests --no-build --filter FullyQualifiedName~SeedSweep --logger "console;verbosity=detailed" | grep SWEEP</c>
/// (<c>RTS_SWEEP_N</c> sets the seed count, default 12; <c>RTS_SWEEP_FROM</c> the first k, default 1; <c>RTS_SWEEP_OUT</c> also appends each line to a file).
/// </summary>
public sealed class SeedSweepTests
{
    private readonly ITestOutputHelper _out;
    public SeedSweepTests(ITestOutputHelper output) => _out = output;

    /// <summary>The mission seed of sweep run <paramref name="k"/> (1-based), as in the session 20 baseline.</summary>
    internal static ulong SeedOf(int k) => (ulong)(1000 + k * 7919);

    [Fact]
    public void SeedSweep()
    {
        string? spec = Environment.GetEnvironmentVariable("RTS_SWEEP");
        if (string.IsNullOrWhiteSpace(spec)) return;
        int n = int.Parse(Environment.GetEnvironmentVariable("RTS_SWEEP_N") ?? "12");
        int from = int.Parse(Environment.GetEnvironmentVariable("RTS_SWEEP_FROM") ?? "1");
        string root = Path.Combine(Path.GetTempPath(), "rts_seed_sweep_" + Environment.ProcessId);
        try
        {
            foreach (string c in spec.Split(','))
            {
                string id = c.Split(':')[0].Trim();
                int tier = int.Parse(c.Split(':')[1]);
                int wins = 0;
                var ticks = new List<string>();
                for (int k = from; k < from + n; k++)
                {
                    string dir = SeedDir(root, k);
                    WalkResult r = CampaignWalks.Play(id, tier, MissionSession.Create(dir, id));
                    bool ok = r.Won && r.Tick < CampaignWalks.TickLimit(id);
                    if (ok) wins++;
                    ticks.Add(ok ? r.Tick.ToString() : "L");
                }
                string line = $"SWEEP {id} t{tier} wins {wins}/{n} [{string.Join(" ", ticks)}]";
                _out.WriteLine(line);
                string? outFile = Environment.GetEnvironmentVariable("RTS_SWEEP_OUT");   // the laptop script collects lines here
                if (!string.IsNullOrEmpty(outFile)) File.AppendAllText(outFile, line + Environment.NewLine);
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>A data folder with copies of what a mission session reads (rules, maps, missions), whose mission.toml files get seed k.
    /// S21: copies, not links, so it also runs on Windows without Developer Mode (tools/measure/run_s21_campaign_sweep.ps1).</summary>
    private static string SeedDir(string root, int k)
    {
        string dir = Path.Combine(root, "s" + k);
        if (Directory.Exists(dir)) return dir;
        string src = TestData.DataDir;
        foreach (string sub in new[] { "rules", "maps", "missions" }) CopyTree(Path.Combine(src, sub), Path.Combine(dir, sub));
        foreach (string toml in Directory.EnumerateFiles(Path.Combine(dir, "missions"), "mission.toml", SearchOption.AllDirectories))
            File.WriteAllText(toml, Regex.Replace(File.ReadAllText(toml), @"(?m)^seed = \d+", $"seed = {SeedOf(k)}"));
        return dir;
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (string f in Directory.EnumerateFiles(from)) File.Copy(f, Path.Combine(to, Path.GetFileName(f)), true);
        foreach (string d in Directory.EnumerateDirectories(from)) CopyTree(d, Path.Combine(to, Path.GetFileName(d)));
    }
}
