// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Host;
using Rts.Game.Sandbox;
using Rts.Game.Saves;
using Rts.Game.Screens;
using Rts.Game.Tests.Saves;
using Rts.Game.Ui.Spectator;

namespace Rts.Game.Tests.S44;

/// <summary>
/// M16-A-02 (s43 audit AI-06): a broken data/ai/difficulty.toml is reported by the entry points that load it, not swallowed into
/// the default AI profile. Each test copies the real data folder (media left out) into scratch/ and puts a malformed file beside the rules.
/// </summary>
public sealed class DifficultyErrorTests
{
    /// <summary>A synthetic, unterminated table: the TOML parser rejects it, so the loader throws a <see cref="RulesException"/>.</summary>
    private const string MalformedToml = "[recruit\nattack_threshold = 1\n";

    [Fact]
    public void Skirmish_create_reports_a_broken_difficulty_file()
    {
        using var data = new BrokenDifficultyData();
        RulesException e = Assert.Throws<RulesException>(() => SkirmishGame.Create(data.RulesDir, 1UL, 1));
        Assert.Contains("difficulty.toml", e.Message);
    }

    [Fact]
    public void Spectator_create_reports_a_broken_difficulty_file()
    {
        using var data = new BrokenDifficultyData();
        RulesException e = Assert.Throws<RulesException>(() => SpectatorSetup.Create(data.RulesDir, 1UL, 1));
        Assert.Contains("difficulty.toml", e.Message);
    }

    [Fact]
    public void Mission_create_reports_a_broken_difficulty_file()
    {
        using var data = new BrokenDifficultyData();
        RulesException e = Assert.Throws<RulesException>(() => MissionGame.Create(data.RulesDir, "auth_01", 1));
        Assert.Contains("difficulty.toml", e.Message);
    }

    [Fact]
    public void Mission_ai_hooks_report_a_broken_difficulty_file()
    {
        using var data = new BrokenDifficultyData();
        RulesException e = Assert.Throws<RulesException>(() => MissionAiHooks.LoadTiers(data.RulesDir));
        Assert.Contains("difficulty.toml", e.Message);
    }

    [Fact]
    public void Loading_a_save_reports_a_broken_difficulty_file()
    {
        using var t = new SaveTestSupport();
        (SimHost host, SkirmishGame game) = t.NewSkirmish(99);
        string path = GameSaver.Save(t.Catalog, game.SaveTemplate(SkirmishSettings.Default), host, t.RulesDir, "slot");

        using var data = new BrokenDifficultyData();   // same rules, broken difficulty file
        Exception e = Assert.ThrowsAny<Exception>(() => GameLoader.Load(path, data.RulesDir));
        Assert.Contains("difficulty.toml", e.Message);
    }

    [Fact]
    public void The_real_difficulty_file_still_loads_five_tiers()
    {
        string rules = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
        Assert.Equal(DifficultyLoader.TierIds.Count, MissionAiHooks.LoadTiers(rules).Count);
    }

    /// <summary>A scratch copy of the data folder whose ai/difficulty.toml is replaced by <see cref="MalformedToml"/>.</summary>
    private sealed class BrokenDifficultyData : IDisposable
    {
        // Folders the loaders never read in these tests; copying them would only slow the run.
        private static readonly HashSet<string> MediaFolders =
            new(StringComparer.OrdinalIgnoreCase) { "sprites", "sprites_classic", "audio", "music", "images", "portraits", "theaters", "fonts" };

        public BrokenDifficultyData()
        {
            string realRules = RulesLocator.FindRulesDir(AppContext.BaseDirectory) ?? throw new InvalidOperationException("data/rules not found");
            string realData = Path.GetDirectoryName(Path.GetFullPath(realRules))!;
            Folder = Path.Combine(RepoRoot(), "scratch", "s44", "difficulty-" + Guid.NewGuid().ToString("N"));
            string data = Path.Combine(Folder, "data");
            CopyTree(realData, data, top: true);
            Directory.CreateDirectory(Path.Combine(data, "ai"));
            File.WriteAllText(Path.Combine(data, "ai", DifficultyLoader.FileName), MalformedToml);
            RulesDir = Path.Combine(data, "rules");
        }

        public string Folder { get; }
        public string RulesDir { get; }

        public void Dispose()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        }

        private static void CopyTree(string from, string to, bool top)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
            foreach (string dir in Directory.GetDirectories(from))
            {
                string name = Path.GetFileName(dir);
                if (top && MediaFolders.Contains(name)) continue;
                CopyTree(dir, Path.Combine(to, name), top: false);
            }
        }

        private static string RepoRoot()
        {
            string? dir = AppContext.BaseDirectory;
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir, "RTS_2027.slnx"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }

            throw new FileNotFoundException("RTS_2027.slnx not found");
        }
    }
}
