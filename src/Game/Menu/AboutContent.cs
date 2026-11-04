// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>The text of the About page: the GPL v3 notice and the third-party credits.</summary>
public static class AboutContent
{
    /// <summary>The licence notice (the standard GPL v3 "how to apply" wording plus the MonoGame permission).</summary>
    public const string Notice =
        "RTS_2027 is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as "
        + "published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.\n\n"
        + "This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of "
        + "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details: "
        + "https://www.gnu.org/licenses/gpl-3.0.html\n\n"
        + "The LICENSE file next to the game adds a permission to link with MonoGame. The game contains no Electronic Arts code, "
        + "trademarks or assets.";

    /// <summary>Credits used when docs/CREDITS.md is not next to the game (a published build). Keep in step with that file.</summary>
    public static readonly IReadOnlyList<string> FallbackCredits = new[]
    {
        "MonoGame.Framework.DesktopGL 3.8.6 - Microsoft Public License (Ms-PL), some MIT portions",
        "SDL 2.32.10 (SDL2.dll) - zlib",
        "OpenAL Soft 1.24.3 (openal.dll) - LGPL 2, dynamically loaded",
        "NVorbis 0.10.4 - MIT",
        "Tomlyn 2.10.1 - BSD-2-Clause",
        "MoonSharp 2.0.0 - BSD-3-Clause",
        "System.IO.Hashing 10.0.14 - MIT",
        ".NET runtime 10.0.14 - MIT",
        "Noto Sans Regular and Bold 2.004, Copyright 2015 Google LLC - SIL Open Font License 1.1",
    };

    /// <summary>
    /// Reads the credit lines out of the markdown of docs/CREDITS.md: each table row becomes <c>Name - licence</c>, text in brackets and
    /// backticks cleaned up. Returns the fallback list when the text has no rows.
    /// </summary>
    public static IReadOnlyList<string> ParseCredits(string? markdown)
    {
        var lines = new List<string>();
        if (markdown is not null)
        {
            foreach (string raw in markdown.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith('|') || line.StartsWith("|---") || line.StartsWith("| ---")) continue;
                string[] cells = line.Trim('|').Split('|').Select(c => Clean(c)).ToArray();
                if (cells.Length < 2 || cells[0] is "Library" or "Asset" or "(none yet)" || cells[0].Length == 0) continue;
                lines.Add(cells[0].Length > 78 ? cells[0][..75] + "..." : cells[0] + " - " + Short(cells[1]));
            }
        }
        return lines.Count > 0 ? lines : FallbackCredits;
    }

    private static string Clean(string cell) => cell.Replace("`", "").Replace("**", "").Trim();

    // The licence cell can be a paragraph; keep the part before the first ';' or '.' break.
    private static string Short(string licence)
    {
        int cut = licence.IndexOfAny(new[] { ';', '.' });
        string s = cut > 8 ? licence[..cut] : licence;
        return s.Length > 70 ? s[..67] + "..." : s;
    }
}
