// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Screens;

/// <summary>The credits (M10-20), read from <c>data/ui/credits.toml</c>; a short form of docs/CREDITS.md, kept in step with it by a test.</summary>
/// <param name="Title">Top title.</param>
/// <param name="Subtitle">Line under the title.</param>
/// <param name="Closing">Last line.</param>
/// <param name="Sections">The sections, in order.</param>
public sealed record CreditsDoc(string Title, string Subtitle, string Closing, IReadOnlyList<CreditsSection> Sections)
{
    /// <summary>The file under the data folder (the folder above data/rules).</summary>
    public static string PathFor(string rulesDir) => Path.Combine(rulesDir, "..", "ui", "credits.toml");

    /// <summary>Reads the file; a missing or invalid file gives <see cref="Fallback"/>.</summary>
    public static CreditsDoc Load(string rulesDir)
    {
        try
        {
            string path = PathFor(rulesDir);
            return File.Exists(path) ? Parse(File.ReadAllText(path)) ?? Fallback : Fallback;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Fallback;
        }
    }

    /// <summary>What is shown when the file cannot be read.</summary>
    public static CreditsDoc Fallback { get; } = new("RTS_2027", "Credits", "Thank you for playing.", Array.Empty<CreditsSection>());

    /// <summary>Parses credits text; null when it is not valid TOML.</summary>
    public static CreditsDoc? Parse(string toml)
    {
        TomlTable? root;
        try { root = TomlSerializer.Deserialize<TomlTable>(toml); }
        catch (TomlException) { return null; }
        if (root is null) return null;
        var sections = new List<CreditsSection>();
        if (root.TryGetValue("section", out object? rows) && rows is TomlTableArray array)
        {
            foreach (TomlTable row in array)
            {
                var entries = new List<CreditsEntry>();
                if (row.TryGetValue("entry", out object? list) && list is TomlTableArray items)
                {
                    foreach (TomlTable item in items)
                    {
                        string name = Str(item, "name");
                        if (name.Length == 0) continue;
                        var match = new List<string>();
                        if (item.TryGetValue("match", out object? m) && m is TomlArray marr)
                            foreach (object? x in marr) if (x is string text && text.Length > 0) match.Add(text);
                        entries.Add(new CreditsEntry(name, Str(item, "credit"), match));
                    }
                }

                sections.Add(new CreditsSection(Str(row, "heading"), entries));
            }
        }

        return new CreditsDoc(Str(root, "title", "RTS_2027"), Str(root, "subtitle", "Credits"), Str(root, "closing"), sections);
    }

    /// <summary>
    /// The lines of the roll for a width: title, then each section (heading, then each name with its wrapped credit text), then the closing line.
    /// </summary>
    /// <param name="width">Text width in the unit <paramref name="measure"/> returns.</param>
    /// <param name="measure">Width of a text.</param>
    public IReadOnlyList<CreditsLine> Lines(int width, Func<string, int> measure)
    {
        var lines = new List<CreditsLine> { new(CreditsLineKind.Title, Title) };
        if (Subtitle.Length > 0) lines.Add(new CreditsLine(CreditsLineKind.Text, Subtitle));
        foreach (CreditsSection section in Sections)
        {
            lines.Add(new CreditsLine(CreditsLineKind.Blank, ""));
            lines.Add(new CreditsLine(CreditsLineKind.Blank, ""));
            lines.Add(new CreditsLine(CreditsLineKind.Heading, section.Heading));
            foreach (CreditsEntry entry in section.Entries)
            {
                lines.Add(new CreditsLine(CreditsLineKind.Blank, ""));
                lines.Add(new CreditsLine(CreditsLineKind.Name, entry.Name));
                foreach (string wrapped in Menu.TextWrap.Wrap(entry.Credit, width, measure)) lines.Add(new CreditsLine(CreditsLineKind.Text, wrapped));
            }
        }

        if (Closing.Length > 0)
        {
            lines.Add(new CreditsLine(CreditsLineKind.Blank, ""));
            lines.Add(new CreditsLine(CreditsLineKind.Blank, ""));
            lines.Add(new CreditsLine(CreditsLineKind.Heading, Closing));
        }

        return lines;
    }

    private static string Str(TomlTable t, string key, string fallback = "") =>
        t.TryGetValue(key, out object? v) && v is string s ? s : fallback;
}
