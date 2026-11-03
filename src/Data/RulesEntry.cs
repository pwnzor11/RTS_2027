// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Tomlyn.Model;

namespace Rts.Data;

/// <summary>
/// One TOML table after <c>base</c> resolution: its id, the file it came from, the merged values (inherited
/// first, then overridden) and the keys the entry sets itself.
/// </summary>
internal sealed record RulesEntry(string Id, string File, IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<string> OwnKeys)
{
    /// <summary>Wraps a table that has no inheritance.</summary>
    public static RulesEntry FromTable(string id, string file, TomlTable table)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        var own = new List<string>();
        foreach (var kv in table)
        {
            values[kv.Key] = kv.Value;
            own.Add(kv.Key);
        }
        return new RulesEntry(id, file, values, own);
    }
}
