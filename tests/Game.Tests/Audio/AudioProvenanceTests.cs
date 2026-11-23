// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Tests.Audio;

/// <summary>
/// data/audio/PROVENANCE.toml (M13-47): every .wav / .ogg under data/audio and data/music has exactly one entry, every entry
/// names a file that exists, and every source and licence comes from the allowed list (the same list as tools/audio/check_provenance.py).
/// </summary>
public sealed class AudioProvenanceTests
{
    private const string UnknownSource = "unknown";
    private const string UnknownLicence = "unknown — ask owner";

    /// <summary>The allowed source and the licence wording it must carry (docs/CREDITS.md, Audio table).</summary>
    private static readonly Dictionary<string, string> LicenceBySource = new(StringComparer.Ordinal)
    {
        ["sfxr"] = "CC0 (ASSUMED, owner to confirm)",
        ["stable-audio-open-1.0"] = "Stability AI Community License; shipped under GPL-3.0",
        ["ace-step-1.5"] = "CC0",
        ["kokoro-82m"] = "Apache-2.0 model; output CC0 (ASSUMED)",
        [UnknownSource] = UnknownLicence,
    };

    private sealed record Entry(string? Path, string? Source, string? Licence);

    private static string DataDir() => Path.Combine(AudioTestSupport.RepoRoot(), "data");

    private static List<Entry> LoadEntries()
    {
        string text = File.ReadAllText(Path.Combine(DataDir(), "audio", "PROVENANCE.toml"));
        TomlTable root = TomlSerializer.Deserialize<TomlTable>(text) ?? new TomlTable();
        var entries = new List<Entry>();
        if (root.TryGetValue("file", out object? list) && list is System.Collections.IEnumerable items)
        {
            foreach (object item in items)
            {
                if (item is TomlTable t)
                {
                    entries.Add(new Entry(Text(t, "path"), Text(t, "source"), Text(t, "licence")));
                }
            }
        }
        return entries;
    }

    private static string? Text(TomlTable t, string key) => t.TryGetValue(key, out object? value) ? value as string : null;

    /// <summary>Every .wav / .ogg under data/audio and data/music, relative to data/ with forward slashes.</summary>
    private static HashSet<string> MediaFiles()
    {
        string data = DataDir();
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (string top in new[] { "audio", "music" })
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(data, top), "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".wav", StringComparison.Ordinal) || file.EndsWith(".ogg", StringComparison.Ordinal))
                {
                    files.Add(Path.GetRelativePath(data, file).Replace('\\', '/'));
                }
            }
        }
        return files;
    }

    [Fact]
    public void ManifestExistsAndHasEntries()
    {
        Assert.NotEmpty(LoadEntries());
    }

    [Fact]
    public void EveryAudioFileHasExactlyOneEntry()
    {
        List<Entry> entries = LoadEntries();
        var counts = entries.GroupBy(e => e.Path ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        List<string> missing = MediaFiles().Where(f => !counts.ContainsKey(f))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        List<string> duplicated = counts.Where(kv => kv.Value > 1 && kv.Key.Length > 0)
            .Select(kv => kv.Key).OrderBy(f => f, StringComparer.Ordinal).ToList();

        Assert.Empty(missing);
        Assert.Empty(duplicated);
    }

    [Fact]
    public void EveryEntryHasAllKeysAndAnExistingFile()
    {
        List<Entry> entries = LoadEntries();
        List<string> incomplete = entries
            .Where(e => e.Path is null || e.Source is null || e.Licence is null)
            .Select(e => e.Path ?? "(no path)")
            .ToList();
        List<string> noFile = entries
            .Where(e => e.Path is not null && !File.Exists(Path.Combine(DataDir(), e.Path)))
            .Select(e => e.Path!)
            .ToList();

        Assert.Empty(incomplete);
        Assert.Empty(noFile);
    }

    [Fact]
    public void SourcesAndLicencesComeFromTheAllowedList()
    {
        List<string> bad = LoadEntries()
            .Where(e => e.Source is not null && e.Licence is not null
                && (!LicenceBySource.TryGetValue(e.Source, out string? expected) || expected != e.Licence))
            .Select(e => $"{e.Path}: source '{e.Source}' with licence '{e.Licence}'")
            .ToList();

        Assert.Empty(bad);
    }

    [Fact(Skip = "PENDING Q-87: owner to confirm the sources of the files listed in data/audio/PROVENANCE.toml as unknown")]
    public void UnknownSourcesAreReported()
    {
        List<string> unknown = LoadEntries()
            .Where(e => e.Source == UnknownSource)
            .Select(e => e.Path ?? "(no path)")
            .ToList();

        Assert.Empty(unknown);
    }
}
