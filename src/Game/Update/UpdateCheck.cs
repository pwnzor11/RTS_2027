// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.Json;

namespace Rts.Game.Update;

/// <summary>
/// The pure parts of the update notice (M10-23): reading a version string, comparing two versions, and picking the tag and page
/// address out of the GitHub "latest release" JSON. No network and no clock, so it is unit-tested with fixed text.
/// </summary>
public static class UpdateCheck
{
    /// <summary>The address asked for the latest release.</summary>
    public const string LatestReleaseUrl = "https://api.github.com/repos/pwnzor11/RTS_2027/releases/latest";

    /// <summary>Release pages must start with this, so a odd reply cannot put another address on the menu.</summary>
    public const string ReleasePagePrefix = "https://github.com/";

    /// <summary>
    /// Reads "v1.2.3", "1.2.3", "0.1.5+3f2a9c1" or "1.2.3-beta.1" into its numbers and whether it is a pre-release.
    /// Returns false for anything else (for example "dev").
    /// </summary>
    public static bool TryParseVersion(string? text, out (int Major, int Minor, int Patch, bool Pre) version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        int plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        bool pre = false;
        int dash = s.IndexOf('-');
        if (dash >= 0)
        {
            pre = true;
            s = s[..dash];
        }

        string[] parts = s.Split('.');
        if (parts.Length is < 2 or > 3) return false;
        var n = new int[3];
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out n[i])) return false;
        version = (n[0], n[1], n[2], pre);
        return true;
    }

    /// <summary>True when <paramref name="latestTag"/> is a newer version than <paramref name="current"/>. Unreadable text on either side gives false.</summary>
    public static bool IsNewer(string? latestTag, string? current)
    {
        if (!TryParseVersion(latestTag, out var l) || !TryParseVersion(current, out var c)) return false;
        if (l.Pre) return false;   // the latest-release call never returns one; ignore it if it does
        int cmp = (l.Major, l.Minor, l.Patch).CompareTo((c.Major, c.Minor, c.Patch));
        if (cmp != 0) return cmp > 0;
        return c.Pre;   // 1.0.0 is newer than 1.0.0-beta
    }

    /// <summary>
    /// Reads the tag and page address from the GitHub release JSON and returns the notice when the tag is newer than
    /// <paramref name="current"/>; null when it is not newer, or the JSON is not what was expected.
    /// </summary>
    public static UpdateInfo? FromReleaseJson(string? json, string current)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("tag_name", out JsonElement tagEl) || tagEl.ValueKind != JsonValueKind.String) return null;
            if (!root.TryGetProperty("html_url", out JsonElement urlEl) || urlEl.ValueKind != JsonValueKind.String) return null;
            string? tag = tagEl.GetString(), url = urlEl.GetString();
            if (tag is null || url is null || !url.StartsWith(ReleasePagePrefix, StringComparison.Ordinal)) return null;
            if (!IsNewer(tag, current)) return null;
            return new UpdateInfo(tag.Trim().TrimStart('v', 'V'), url);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The menu line for a found update, for example "New version 0.2.0 available: https://github.com/...".</summary>
    public static string NoticeText(UpdateInfo info) => $"New version {info.Version} available: {info.Url}";
}
