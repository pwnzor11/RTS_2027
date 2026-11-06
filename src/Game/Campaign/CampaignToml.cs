// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn;
using Tomlyn.Model;

namespace Rts.Game.Campaign;

/// <summary>Small typed readers shared by the campaign and ending file parsers. Every failure is an <see cref="InvalidDataException"/>.</summary>
internal static class CampaignToml
{
    public static TomlTable Root(string text)
    {
        try { return TomlSerializer.Deserialize<TomlTable>(text) ?? throw new InvalidDataException("empty file"); }
        catch (Exception e) when (e is not InvalidDataException) { throw new InvalidDataException(e.Message, e); }
    }

    public static TomlTable Table(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is TomlTable r ? r : throw new InvalidDataException($"missing table [{key}]");

    public static string Str(TomlTable t, string key) =>
        t.TryGetValue(key, out object? v) && v is string { Length: > 0 } s ? s : throw new InvalidDataException($"missing or empty string '{key}'");

    public static string OptStr(TomlTable t, string key) =>
        !t.TryGetValue(key, out object? v) ? "" : v is string s ? s : throw new InvalidDataException($"'{key}' must be a string");

    public static IReadOnlyList<string> Strings(TomlTable t, string key)
    {
        if (!t.TryGetValue(key, out object? v)) return Array.Empty<string>();
        if (v is not TomlArray a || !a.All(x => x is string { Length: > 0 })) throw new InvalidDataException($"'{key}' must be a list of ids");
        return a.Select(x => (string)x!).ToList();
    }
}
