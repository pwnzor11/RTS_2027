// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text.RegularExpressions;

namespace Rts.Game.Ui;

/// <summary>A run of text in one weight.</summary>
/// <param name="Text">The text, markers removed.</param>
/// <param name="Bold">True for a bold run.</param>
public readonly record struct MdSpan(string Text, bool Bold);

/// <summary>One display line of lightly formatted text: a heading (drawn in the title style) or a line of regular and bold runs.</summary>
/// <param name="Heading">True for a "#" heading.</param>
/// <param name="Spans">The runs, left to right.</param>
public sealed record MdLine(bool Heading, IReadOnlyList<MdSpan> Spans)
{
    /// <summary>The number of characters of all runs together.</summary>
    public int Length => Spans.Sum(s => s.Text.Length);

    /// <summary>The text without any formatting.</summary>
    public string Plain => string.Concat(Spans.Select(s => s.Text));

    /// <summary>An empty line.</summary>
    public static MdLine Blank { get; } = new(false, Array.Empty<MdSpan>());
}

/// <summary>Parses and wraps the small Markdown subset used in briefings: "#" headings, "**bold**", and "*italic*" (shown plain).</summary>
public static class MarkdownLines
{
    private static readonly Regex Italic = new(@"(?<![\*\w])\*([^\*\s][^\*]*?)\*(?![\*\w])", RegexOptions.Compiled);

    /// <summary>Parses one source line.</summary>
    public static MdLine Parse(string raw)
    {
        string t = raw.TrimEnd('\r').Trim();
        if (t.Length == 0) return MdLine.Blank;
        int hashes = 0;
        while (hashes < t.Length && t[hashes] == '#') hashes++;
        if (hashes > 0 && hashes <= 6 && hashes < t.Length && t[hashes] == ' ')
        {
            string h = Italic.Replace(t[(hashes + 1)..].Trim().Replace("**", ""), "$1");
            return new MdLine(true, new[] { new MdSpan(h, true) });
        }

        var spans = new List<MdSpan>();
        string[] parts = t.Split("**");
        for (int i = 0; i < parts.Length; i++)
        {
            string text = Italic.Replace(parts[i], "$1");
            if (text.Length > 0) spans.Add(new MdSpan(text, i % 2 == 1));
        }

        return new MdLine(false, spans);
    }

    /// <summary>Wraps a line at word breaks to at most <paramref name="width"/> characters; weights and the heading flag carry over.</summary>
    public static List<MdLine> Wrap(MdLine line, int width)
    {
        var result = new List<MdLine>();
        if (line.Length == 0) { result.Add(line); return result; }
        var current = new List<MdSpan>();
        int len = 0;
        void Add(string text, bool bold)
        {
            if (current.Count > 0 && current[^1].Bold == bold) current[^1] = new MdSpan(current[^1].Text + text, bold);
            else current.Add(new MdSpan(text, bold));
            len += text.Length;
        }

        foreach (MdSpan span in line.Spans)
        {
            foreach (string piece in Regex.Split(span.Text, "(?<= )"))
            {
                string word = piece.TrimEnd(' ');
                if (word.Length == 0) { if (piece.Length > 0 && len > 0) Add(piece, span.Bold); continue; }
                if (len > 0 && len + word.Length > width)
                {
                    TrimEnd(current);
                    result.Add(new MdLine(line.Heading, current));
                    current = new List<MdSpan>();
                    len = 0;
                }

                Add(piece, span.Bold);
            }
        }

        TrimEnd(current);
        if (current.Count > 0) result.Add(new MdLine(line.Heading, current));
        return result;
    }

    private static void TrimEnd(List<MdSpan> spans)
    {
        while (spans.Count > 0)
        {
            string t = spans[^1].Text.TrimEnd(' ');
            if (t.Length > 0) { spans[^1] = spans[^1] with { Text = t }; return; }
            spans.RemoveAt(spans.Count - 1);
        }
    }
}
