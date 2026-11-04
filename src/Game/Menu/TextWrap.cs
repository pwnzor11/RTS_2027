// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>Word wrapping for the menu text pages. Pure: the width of a string is passed in, so it is tested without fonts.</summary>
public static class TextWrap
{
    /// <summary>
    /// Breaks <paramref name="text"/> into lines no wider than <paramref name="maxWidth"/> (as <paramref name="measure"/> reports),
    /// at spaces. Existing newlines are kept (an empty line stays an empty line). A single word wider than the limit gets its own line.
    /// </summary>
    public static List<string> Wrap(string text, int maxWidth, Func<string, int> measure)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Replace("\r", "").Split('\n'))
        {
            if (paragraph.Length == 0) { lines.Add(""); continue; }
            string current = "";
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string trial = current.Length == 0 ? word : current + " " + word;
                if (current.Length > 0 && measure(trial) > maxWidth)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = trial;
                }
            }
            if (current.Length > 0) lines.Add(current);
        }
        return lines;
    }
}
