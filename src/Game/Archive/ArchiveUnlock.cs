// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Archive;

/// <summary>
/// The unlock rule of an entry, parsed from its TOML string: <c>open</c>, <c>campaign:&lt;mission id&gt;</c>,
/// <c>encounter:&lt;rules id&gt;</c> or <c>research:&lt;upgrade id&gt;</c>.
/// </summary>
/// <param name="Kind">The rule kind.</param>
/// <param name="Arg">The mission or rules id (empty for <see cref="ArchiveUnlockKind.Open"/>).</param>
public readonly record struct ArchiveUnlock(ArchiveUnlockKind Kind, string Arg)
{
    /// <summary>The always-open rule.</summary>
    public static ArchiveUnlock Open => new(ArchiveUnlockKind.Open, "");

    /// <summary>Parses a rule string. Returns false (and <see cref="Open"/>) for anything not understood.</summary>
    public static bool TryParse(string? text, out ArchiveUnlock rule)
    {
        rule = Open;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim();
        if (string.Equals(t, "open", StringComparison.Ordinal)) return true;
        int colon = t.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0 || colon == t.Length - 1) return false;
        string arg = t[(colon + 1)..].Trim();
        ArchiveUnlockKind? kind = t[..colon] switch
        {
            "campaign" => ArchiveUnlockKind.Campaign,
            "encounter" => ArchiveUnlockKind.Encounter,
            "research" => ArchiveUnlockKind.Research,
            _ => null,
        };
        if (kind is null || arg.Length == 0) return false;
        rule = new ArchiveUnlock(kind.Value, arg);
        return true;
    }

    /// <summary>The rule as its TOML string.</summary>
    public override string ToString() => Kind == ArchiveUnlockKind.Open ? "open" : $"{Kind.ToString().ToLowerInvariant()}:{Arg}";

    /// <summary>A one-line hint telling the player how to unlock the entry. <paramref name="nameOf"/> turns an id into a title (or null).</summary>
    public string Hint(Func<string, string?>? nameOf = null)
    {
        string name = nameOf?.Invoke(Arg) ?? Arg;
        return Kind switch
        {
            ArchiveUnlockKind.Open => "",
            ArchiveUnlockKind.Campaign => $"Unlocks in the campaign ({name}).",
            ArchiveUnlockKind.Encounter => $"Unlocks when you meet it: {name}.",
            ArchiveUnlockKind.Research => $"Unlocks when you research {name}.",
            _ => "",
        };
    }
}
