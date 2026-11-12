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
/// (<c>RTS_SWEEP_N</c> sets the seed count, default 12; <c>RTS_SWEEP_FROM</c> the first k, default 1).
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
                _out.WriteLine($"SWEEP {id} t{tier} wins {wins}/{n} [{string.Join(" ", ticks)}]");
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>A data folder that links everything in the real data folder except the missions, whose mission.toml files get seed k.</summary>
    private static string SeedDir(string root, int k)
    {
        string dir = Path.Combine(root, "s" + k);
        if (Directory.Exists(dir)) return dir;
        string src = TestData.DataDir;
        Directory.CreateDirectory(Path.Combine(dir, "missions"));
        foreach (string e in Directory.EnumerateFileSystemEntries(src))
        {
            string name = Path.GetFileName(e);
            if (name == "missions") continue;
            if (Directory.Exists(e)) Directory.CreateSymbolicLink(Path.Combine(dir, name), e);
            else File.CreateSymbolicLink(Path.Combine(dir, name), e);
        }
        foreach (string m in Directory.EnumerateDirectories(Path.Combine(src, "missions")))
        {
            string md = Path.Combine(dir, "missions", Path.GetFileName(m));
            Directory.CreateDirectory(md);
            foreach (string f in Directory.EnumerateFiles(m))
            {
                string target = Path.Combine(md, Path.GetFileName(f));
                if (Path.GetFileName(f) == "mission.toml")
                    File.WriteAllText(target, Regex.Replace(File.ReadAllText(f), @"(?m)^seed = \d+", $"seed = {SeedOf(k)}"));
                else
                    File.CreateSymbolicLink(target, f);
            }
        }
        return dir;
    }
}
