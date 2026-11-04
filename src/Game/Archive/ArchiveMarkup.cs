// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game.Archive;

/// <summary>
/// The simple markup of an entry body: paragraphs are separated by a blank line, a line starting with <c>#</c> or
/// <c>##</c> is a heading, and <c>*text*</c> is emphasis (drawn bold). Parsing and word wrapping are pure so they are
/// testable without a graphics device.
/// </summary>
public static class ArchiveMarkup
{
    /// <summary>A run of text with one style.</summary>
    /// <param name="Text">The characters (words and spaces).</param>
    /// <param name="Emphasis">True between asterisks.</param>
    public readonly record struct Span(string Text, bool Emphasis);

    /// <summary>One paragraph or heading.</summary>
    /// <param name="Heading">True for a heading (all text is one span).</param>
    /// <param name="Spans">The styled runs.</param>
    public sealed record Block(bool Heading, IReadOnlyList<Span> Spans);

    /// <summary>One wrapped output line: a list of positioned words.</summary>
    /// <param name="Heading">True when the line belongs to a heading.</param>
    /// <param name="Words">Words with their x offset from the line start.</param>
    /// <param name="GapBefore">True on the first line of a block after the first (the screen adds paragraph spacing).</param>
    public sealed record Line(bool Heading, IReadOnlyList<(string Text, bool Emphasis, int X)> Words, bool GapBefore);

    /// <summary>Parses body text into blocks.</summary>
    public static IReadOnlyList<Block> Parse(string body)
    {
        var blocks = new List<Block>();
        var paragraph = new StringBuilder();

        void FlushParagraph()
        {
            if (paragraph.Length == 0) return;
            blocks.Add(new Block(false, Spans(paragraph.ToString())));
            paragraph.Clear();
        }

        foreach (string raw in body.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0)
            {
                FlushParagraph();
            }
            else if (line[0] == '#')
            {
                FlushParagraph();
                string text = line.TrimStart('#').Trim();
                if (text.Length > 0) blocks.Add(new Block(true, new[] { new Span(text, false) }));
            }
            else
            {
                if (paragraph.Length > 0) paragraph.Append(' ');
                paragraph.Append(line);
            }
        }
        FlushParagraph();
        return blocks;
    }

    /// <summary>Splits text on asterisks into plain and emphasised spans. An unmatched asterisk is kept as text.</summary>
    public static IReadOnlyList<Span> Spans(string text)
    {
        var spans = new List<Span>();
        int pos = 0;
        while (pos < text.Length)
        {
            int open = text.IndexOf('*', pos);
            int close = open < 0 ? -1 : text.IndexOf('*', open + 1);
            if (open < 0 || close < 0)
            {
                spans.Add(new Span(text[pos..], false));
                break;
            }
            if (open > pos) spans.Add(new Span(text[pos..open], false));
            if (close > open + 1) spans.Add(new Span(text[(open + 1)..close], true));
            pos = close + 1;
        }
        return spans;
    }

    /// <summary>Word-wraps blocks to <paramref name="width"/> pixels using <paramref name="measure"/> (text, emphasis) to width.</summary>
    public static IReadOnlyList<Line> Wrap(IReadOnlyList<Block> blocks, int width, Func<string, bool, int> measure)
    {
        var lines = new List<Line>();
        int space = measure(" ", false);
        for (int b = 0; b < blocks.Count; b++)
        {
            Block block = blocks[b];
            var words = new List<(string Text, bool Emphasis, int X)>();
            int x = 0;
            bool first = true;
            bool pendingSpace = false;

            void Emit()
            {
                lines.Add(new Line(block.Heading, words.ToArray(), first && b > 0));
                first = false;
                words.Clear();
                x = 0;
            }

            foreach (Span span in block.Spans)
            {
                // Spaces at the span edges separate words; a span glued to the previous word (no space) stays glued.
                if (span.Text.Length > 0 && span.Text[0] == ' ') pendingSpace = true;
                foreach (string word in span.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    int w = measure(word, span.Emphasis || block.Heading);
                    int gap = words.Count > 0 && pendingSpace ? space : 0;
                    if (words.Count > 0 && x + gap + w > width) { Emit(); gap = 0; }
                    words.Add((word, span.Emphasis || block.Heading, x + gap));
                    x += gap + w;
                    pendingSpace = true;
                }
                pendingSpace = span.Text.Length > 0 && span.Text[^1] == ' ';
            }
            if (words.Count > 0) Emit();
        }
        return lines;
    }
}
