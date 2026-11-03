// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Rules;
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// Typed, validating reads of one entry's merged values. Every problem names the file and the dotted key
/// (entry id, then key). A read that fails records a problem and returns null, so the caller skips the entry.
/// </summary>
internal sealed class RulesFields
{
    private readonly RulesEntry _entry;
    private readonly List<RulesProblem> _problems;

    public RulesFields(RulesEntry entry, List<RulesProblem> problems)
    {
        _entry = entry;
        _problems = problems;
    }

    /// <summary>Entry id.</summary>
    public string Id => _entry.Id;

    /// <summary>File the entry came from.</summary>
    public string File => _entry.File;

    /// <summary>Records an unknown key for every key the entry sets itself that is not in <paramref name="allowed"/>.</summary>
    public void CheckKeys(params string[] allowed)
    {
        foreach (string key in _entry.OwnKeys)
        {
            if (Array.IndexOf(allowed, key) < 0)
            {
                Problem(key, "unknown key");
            }
        }
    }

    /// <summary>Records a problem for <c>Id.key</c> in this entry's file.</summary>
    public void Problem(string key, string message) =>
        _problems.Add(new RulesProblem(File, Id + "." + key, message));

    /// <summary>Reads a required integer in [min, max].</summary>
    public int? Int(string key, int min, int max)
    {
        if (!TryGet(key, out object? raw))
        {
            return null;
        }
        if (raw is not long value)
        {
            Problem(key, $"expected an integer, got {TypeName(raw)}");
            return null;
        }
        if (value < min || value > max)
        {
            Problem(key, $"must be between {min} and {max}, got {value}");
            return null;
        }
        return (int)value;
    }

    /// <summary>Reads a required array of exactly <paramref name="count"/> integers, each in [min, max].</summary>
    public int[]? IntArray(string key, int count, int min, int max)
    {
        if (!TryGet(key, out object? raw))
        {
            return null;
        }
        if (raw is not TomlArray array)
        {
            Problem(key, $"expected an array, got {TypeName(raw)}");
            return null;
        }
        if (array.Count != count)
        {
            Problem(key, $"expected {count} values, got {array.Count}");
            return null;
        }
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (array[i] is not long value)
            {
                Problem(key, $"entry {i} must be an integer, got {TypeName(array[i] ?? "")}");
                return null;
            }
            if (value < min || value > max)
            {
                Problem(key, $"entry {i} must be between {min} and {max}, got {value}");
                return null;
            }
            result[i] = (int)value;
        }
        return result;
    }

    /// <summary>Reads a required boolean.</summary>
    public bool? Bool(string key)
    {
        if (!TryGet(key, out object? raw))
        {
            return null;
        }
        if (raw is not bool value)
        {
            Problem(key, $"expected a boolean, got {TypeName(raw)}");
            return null;
        }
        return value;
    }

    /// <summary>Reads a required string (may be empty).</summary>
    public string? Str(string key)
    {
        if (!TryGet(key, out object? raw))
        {
            return null;
        }
        if (raw is not string text)
        {
            Problem(key, $"expected a string, got {TypeName(raw)}");
            return null;
        }
        return text;
    }

    /// <summary>Reads a required string that must be one of <paramref name="names"/>, parsed by <paramref name="parse"/>.</summary>
    public T? Choice<T>(string key, Func<string, T?> parse, string[] names) where T : struct
    {
        string? text = Str(key);
        if (text is null)
        {
            return null;
        }
        T? value = parse(text);
        if (value is null)
        {
            Problem(key, $"must be one of {RulesEnums.Join(names)}, got '{text}'");
        }
        return value;
    }

    /// <summary>Reads a required string that must be one of <paramref name="allowed"/>.</summary>
    public string? OneOf(string key, string[] allowed)
    {
        string? text = Str(key);
        if (text is null)
        {
            return null;
        }
        if (Array.IndexOf(allowed, text) < 0)
        {
            Problem(key, $"must be one of {RulesEnums.Join(allowed)}, got '{text}'");
            return null;
        }
        return text;
    }

    /// <summary>Reads a required array of strings.</summary>
    public List<string>? StrList(string key)
    {
        if (!TryGet(key, out object? raw))
        {
            return null;
        }
        if (raw is not TomlArray array)
        {
            Problem(key, $"expected an array, got {TypeName(raw)}");
            return null;
        }
        var list = new List<string>();
        foreach (object? item in array)
        {
            if (item is not string text)
            {
                Problem(key, "expected an array of strings");
                return null;
            }
            list.Add(text);
        }
        return list;
    }

    /// <summary>Reads the weapon named by <c>primary_weapon</c>: -1 for "", otherwise its index in the sorted weapon list.</summary>
    public int? WeaponIndex(IReadOnlyList<WeaponType> weapons)
    {
        string? name = Str("primary_weapon");
        if (name is null)
        {
            return null;
        }
        if (name.Length == 0)
        {
            return -1;
        }
        int index = SimRules.IndexOf(weapons, name, w => w.Id);
        if (index < 0)
        {
            Problem("primary_weapon", $"unknown weapon '{name}'");
            return null;
        }
        return index;
    }

    /// <summary>True if the entry sets <paramref name="key"/> (after inheritance).</summary>
    public bool Has(string key) => _entry.Values.TryGetValue(key, out object? raw) && raw is not null;

    /// <summary>Reads an optional integer in [min, max]; <paramref name="def"/> when absent. Null only after a recorded problem.</summary>
    public int? OptInt(string key, int min, int max, int def) => Has(key) ? Int(key, min, max) : def;

    /// <summary>Reads an optional boolean; <paramref name="def"/> when absent. Null only after a recorded problem.</summary>
    public bool? OptBool(string key, bool def) => Has(key) ? Bool(key) : def;

    /// <summary>Reads an optional string; <paramref name="def"/> when absent. Null only after a recorded problem.</summary>
    public string? OptStr(string key, string def) => Has(key) ? Str(key) : def;

    /// <summary>Reads an optional integer array of exactly <paramref name="count"/> values; <paramref name="def"/> (may be null) when
    /// absent. Check <see cref="Has"/> to tell "absent" from "invalid"; invalid records a problem and returns null.</summary>
    public int[]? OptIntArray(string key, int count, int min, int max, int[]? def) =>
        Has(key) ? IntArray(key, count, min, max) : def;

    private bool TryGet(string key, out object? raw)
    {
        if (!_entry.Values.TryGetValue(key, out raw) || raw is null)
        {
            Problem(key, "missing required key");
            return false;
        }
        return true;
    }

    private static string TypeName(object? value) => value switch
    {
        null => "nothing",
        string => "a string",
        bool => "a boolean",
        long => "an integer",
        double => "a float",
        TomlArray => "an array",
        TomlTable => "a table",
        _ => value.GetType().Name,
    };
}
