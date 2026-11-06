// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Scripting;

/// <summary>
/// The text of one mission by locale key (plan 3.2; D-030: all text of a mission in one locale file). Read from
/// <c>data/missions/&lt;id&gt;/locale.toml</c> by <see cref="MissionLoader"/> (the <c>locale</c> key, schema 2). The file holds flat
/// <c>key = "text"</c> lines (a dotted key is written quoted: <c>"m1.warning" = "..."</c>) and an optional <c>[voice]</c> table that maps
/// a key to <c>"voice/&lt;leader_id&gt;/&lt;key&gt;.ogg"</c> (D-154). A key the file does not have resolves to the key in brackets, so a
/// missing line shows in play and never stops a script. Bad lines are skipped and listed in <see cref="Warnings"/>.
/// </summary>
public sealed class MissionLocale
{
    /// <summary>Longest locale key, in characters (the same limit as the other script ids).</summary>
    public const int MaxKeyLength = 64;

    private readonly Dictionary<string, string> _text;
    private readonly Dictionary<string, string> _voice;
    private readonly List<string> _warnings;
    private readonly List<string> _missing = new();

    private MissionLocale(Dictionary<string, string> text, Dictionary<string, string> voice, List<string> warnings)
    {
        _text = text;
        _voice = voice;
        _warnings = warnings;
    }

    /// <summary>A locale with no lines: every key resolves to itself in brackets. A new instance per call.</summary>
    public static MissionLocale Empty => new(new Dictionary<string, string>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal), new List<string>());

    /// <summary>Problems found while loading (unknown or malformed lines, a missing file). Empty when the file is clean.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Keys the script asked for that the file does not have, in first-asked order (diagnostics, not state).</summary>
    public IReadOnlyList<string> MissingKeys => _missing;

    /// <summary>Number of text lines loaded.</summary>
    public int Count => _text.Count;

    /// <summary>True if the file has a text line for <paramref name="key"/>.</summary>
    public bool Contains(string key) => _text.ContainsKey(key);

    /// <summary>
    /// The text for <paramref name="key"/>, or <c>[key]</c> when the file has none (the miss is recorded in <see cref="MissingKeys"/>).
    /// </summary>
    public string Resolve(string key)
    {
        if (_text.TryGetValue(key, out string? text)) return text;
        if (!_missing.Contains(key)) _missing.Add(key);
        return "[" + key + "]";
    }

    /// <summary>The optional voice file for <paramref name="key"/> ("voice/&lt;leader&gt;/&lt;key&gt;.ogg"), or null.</summary>
    public string? VoiceOf(string key) => _voice.TryGetValue(key, out string? path) ? path : null;

    /// <summary>True for a locale key or id: 1 to 64 characters, no spaces or control characters.</summary>
    public static bool IsValidKey(string key) =>
        key.Length is >= 1 and <= MaxKeyLength && !key.Any(static c => char.IsWhiteSpace(c) || char.IsControl(c));

    /// <summary>Reads a locale file. A missing file gives an empty locale with a warning; a file that is not TOML is an error.</summary>
    /// <exception cref="InvalidDataException">The file is not valid TOML.</exception>
    public static MissionLocale Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string name = Path.GetFileName(path);
        if (!File.Exists(path))
        {
            var empty = Empty;
            empty._warnings.Add(name + ": file not found, every line shows its key in brackets");
            return empty;
        }

        return Parse(File.ReadAllText(path), name);
    }

    /// <summary>Parses locale text. <paramref name="source"/> names the file in warnings and errors.</summary>
    /// <exception cref="InvalidDataException">The text is not valid TOML.</exception>
    public static MissionLocale Parse(string toml, string source)
    {
        ArgumentNullException.ThrowIfNull(toml);
        TomlTable root;
        try { root = TomlSerializer.Deserialize<TomlTable>(toml) ?? new TomlTable(); }
        catch (Exception e) when (e is not InvalidDataException) { throw new InvalidDataException($"{source}: {e.Message}", e); }

        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        var voice = new Dictionary<string, string>(StringComparer.Ordinal);
        var warnings = new List<string>();
        foreach (var entry in root)
        {
            if (entry.Key == "voice") ReadVoice(entry.Value, source, voice, warnings);
            else Flatten(entry.Key, entry.Value, source, text, warnings);
        }

        return new MissionLocale(text, voice, warnings);
    }

    private static void Flatten(string key, object? value, string source, Dictionary<string, string> text, List<string> warnings)
    {
        if (value is TomlTable table)
        {
            foreach (var child in table) Flatten(key + "." + child.Key, child.Value, source, text, warnings);
            return;
        }

        if (!IsValidKey(key))
        {
            warnings.Add($"{source}: '{key}' is not a valid key (1-{MaxKeyLength} characters, no spaces); line skipped");
            return;
        }

        if (value is string s) text[key] = s;
        else warnings.Add($"{source}: '{key}' must be a text string; line skipped");
    }

    private static void ReadVoice(object? value, string source, Dictionary<string, string> voice, List<string> warnings)
    {
        if (value is not TomlTable table)
        {
            warnings.Add($"{source}: [voice] must be a table of key = \"voice/<leader_id>/<key>.ogg\" lines");
            return;
        }

        foreach (var entry in table)
        {
            if (entry.Value is string path && IsVoicePath(path, entry.Key)) voice[entry.Key] = path;
            else warnings.Add($"{source}: voice for '{entry.Key}' must look like \"voice/<leader_id>/{entry.Key}.ogg\"; line skipped");
        }
    }

    /// <summary>True for "voice/&lt;leader&gt;/&lt;key&gt;.ogg" with a leader id and the key's own name.</summary>
    private static bool IsVoicePath(string path, string key)
    {
        if (!IsValidKey(key) || path.Contains('\\') || path.Contains("..", StringComparison.Ordinal)) return false;
        string[] parts = path.Split('/');
        return parts.Length == 3 && parts[0] == "voice" && IsValidKey(parts[1]) && parts[2] == key + ".ogg";
    }
}
