// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Archive;

/// <summary>
/// Every entry of data/archive/*.toml, in file-name order then file order. Each file holds <c>[[entry]]</c> tables with
/// the keys id, category, faction, title, subtitle, field_note, body, sprite, unlock. Loading is strict (authored data): a bad file
/// throws <see cref="InvalidDataException"/> naming the file and entry.
/// </summary>
public sealed class ArchiveCatalog
{
    private readonly Dictionary<string, ArchiveEntry> _byId;

    private ArchiveCatalog(IReadOnlyList<ArchiveEntry> entries)
    {
        Entries = entries;
        _byId = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    /// <summary>An empty catalog (used when the data folder is missing).</summary>
    public static ArchiveCatalog Empty { get; } = new(Array.Empty<ArchiveEntry>());

    /// <summary>All entries in authored order.</summary>
    public IReadOnlyList<ArchiveEntry> Entries { get; }

    /// <summary>The entry with this id, or null.</summary>
    public ArchiveEntry? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>Entries of one category in authored order.</summary>
    public IEnumerable<ArchiveEntry> InCategory(ArchiveCategory category) => Entries.Where(e => e.Category == category);

    /// <summary>The archive folder next to a rules folder (<c>data/rules</c> gives <c>data/archive</c>).</summary>
    public static string DirectoryFor(string rulesDir) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(rulesDir)) ?? ".", "archive");

    /// <summary>Loads every <c>*.toml</c> in the folder.</summary>
    /// <exception cref="DirectoryNotFoundException">The folder is missing.</exception>
    /// <exception cref="InvalidDataException">A file or entry is invalid, or an id repeats.</exception>
    public static ArchiveCatalog Load(string dir)
    {
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException($"archive folder not found: {dir}");
        var files = Directory.EnumerateFiles(dir, "*.toml").OrderBy(p => p, StringComparer.Ordinal).ToList();
        var entries = new List<ArchiveEntry>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string file in files)
        {
            foreach (ArchiveEntry entry in ParseFile(file))
            {
                if (seen.TryGetValue(entry.Id, out string? first))
                {
                    throw new InvalidDataException($"archive id '{entry.Id}' is defined twice: {first} and {file}");
                }
                seen.Add(entry.Id, file);
                entries.Add(entry);
            }
        }
        return new ArchiveCatalog(entries);
    }

    /// <summary>Parses one TOML file.</summary>
    public static IReadOnlyList<ArchiveEntry> ParseFile(string file)
    {
        TomlTable root;
        try
        {
            root = TomlSerializer.Deserialize<TomlTable>(File.ReadAllText(file)) ?? new TomlTable();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new InvalidDataException($"{file}: {e.Message}", e);
        }

        var result = new List<ArchiveEntry>();
        if (!root.TryGetValue("entry", out object? raw)) return result;
        if (raw is not TomlTableArray array) throw new InvalidDataException($"{file}: 'entry' must be an array of tables ([[entry]])");
        foreach (TomlTable t in array) result.Add(ParseEntry(t, file));
        return result;
    }

    private static ArchiveEntry ParseEntry(TomlTable t, string file)
    {
        string id = Str(t, "id");
        if (id.Length == 0) throw new InvalidDataException($"{file}: an entry has no id");
        string where = $"{file}: entry '{id}'";

        if (!Enum.TryParse(Str(t, "category"), ignoreCase: true, out ArchiveCategory category) || !Enum.IsDefined(category))
        {
            throw new InvalidDataException($"{where}: unknown category '{Str(t, "category")}'");
        }
        ArchiveFaction faction = Str(t, "faction").ToLowerInvariant() switch
        {
            "authority" => ArchiveFaction.Authority,
            "directorate" => ArchiveFaction.Directorate,
            "neutral" or "" => ArchiveFaction.Neutral,
            var other => throw new InvalidDataException($"{where}: unknown faction '{other}'"),
        };
        string unlockText = t.ContainsKey("unlock") ? Str(t, "unlock") : "open";
        if (!ArchiveUnlock.TryParse(unlockText, out ArchiveUnlock unlock))
        {
            throw new InvalidDataException($"{where}: bad unlock rule '{unlockText}'");
        }
        string title = Str(t, "title");
        if (title.Length == 0) throw new InvalidDataException($"{where}: no title");
        return new ArchiveEntry(id, category, faction, title, Str(t, "subtitle"), Str(t, "field_note"), Str(t, "body").Trim(), Str(t, "sprite"),
            unlock, file);
    }

    private static string Str(TomlTable t, string key) => t.TryGetValue(key, out object? v) && v is string s ? s : "";
}
