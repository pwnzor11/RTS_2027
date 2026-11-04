// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Globalization;
using System.Text;
using Rts.Llm;

namespace Rts.Game.Ui.Llm;

/// <summary>
/// The rules of the commander chat panel (M5-07): which lines show, whether the input line is open, what was typed and which
/// commander it goes to. Holds no MonoGame types, so it is tested without a window.
/// </summary>
public sealed class CommanderChatModel
{
    /// <summary>Longest text the player can type.</summary>
    public const int MaxInputChars = 200;

    /// <summary>Lines shown while typing.</summary>
    public const int TypingLineCount = 8;

    /// <summary>Game ticks a line stays in the closed panel (20 s at 20 ticks per second).</summary>
    public const int FadeTicks = 400;

    private readonly List<CommanderLine> _lines = new();
    private readonly StringBuilder _input = new();
    private int _version = -1;

    /// <summary>True while the input line is open.</summary>
    public bool Typing { get; private set; }

    /// <summary>The text typed so far.</summary>
    public string Input => _input.ToString();

    /// <summary>A one-line complaint about the last send (unknown slot, empty text), or null.</summary>
    public string? Notice { get; private set; }

    /// <summary>The log lines as of the last <see cref="Refresh"/>, oldest first.</summary>
    public IReadOnlyList<CommanderLine> Lines => _lines;

    /// <summary>Copies the log again only when its version changed.</summary>
    public void Refresh(CommanderChatLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Version == _version) return;
        log.CopyTo(_lines);
        _version = log.Version;
    }

    /// <summary>Opens the input line. Returns false (stays closed) when there are no commanders.</summary>
    public bool Open(IReadOnlyList<ICommanderChannel> commanders)
    {
        ArgumentNullException.ThrowIfNull(commanders);
        if (Typing) return true;
        if (commanders.Count == 0) return false;
        Typing = true;
        _input.Clear();
        Notice = null;
        return true;
    }

    /// <summary>Closes the input and drops what was typed.</summary>
    public void Cancel()
    {
        Typing = false;
        _input.Clear();
        Notice = null;
    }

    /// <summary>Adds a typed character (control characters and characters past <see cref="MaxInputChars"/> are dropped).</summary>
    public void Type(char c)
    {
        if (!Typing || char.IsControl(c) || _input.Length >= MaxInputChars) return;
        _input.Append(c);
    }

    /// <summary>Removes the last typed character, if any.</summary>
    public void Backspace()
    {
        if (_input.Length > 0) _input.Length--;
    }

    /// <summary>
    /// Sends the typed text: calls <see cref="ICommanderChannel.Hear"/> on the addressed commander and closes the input. On a
    /// complaint (unknown slot, empty text) the input stays open with <see cref="Notice"/> set. Returns true when sent.
    /// </summary>
    public bool Submit(IReadOnlyList<ICommanderChannel> commanders, int tick)
    {
        ArgumentNullException.ThrowIfNull(commanders);
        if (!Typing) return false;
        var target = Parse(Input, commanders, out string? notice);
        if (target is null)
        {
            Notice = notice;
            return false;
        }
        Cancel();
        target.Value.Channel.Hear(tick, target.Value.Text);
        return true;
    }

    /// <summary>
    /// Works out where typed text goes. "/to N text" addresses the commander of player slot N (1-based, as the panel shows it).
    /// Any other text goes to the first commander. Returns null with a <paramref name="notice"/> when it cannot be sent.
    /// </summary>
    public static (ICommanderChannel Channel, string Text)? Parse(string input, IReadOnlyList<ICommanderChannel> commanders, out string? notice)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(commanders);
        notice = null;
        if (commanders.Count == 0)
        {
            notice = CommanderChatText.NoCommander;
            return null;
        }

        string text = input.Trim();
        if (text.Length == 0) return null;

        if (text.StartsWith("/to", StringComparison.OrdinalIgnoreCase) && (text.Length == 3 || char.IsWhiteSpace(text[3])))
        {
            string rest = text[3..].TrimStart();
            int end = 0;
            while (end < rest.Length && !char.IsWhiteSpace(rest[end])) end++;
            string slotText = rest[..end];
            string body = rest[end..].Trim();

            if (!int.TryParse(slotText, NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot < 1)
            {
                notice = string.Format(CultureInfo.InvariantCulture, CommanderChatText.UnknownSlot, slotText);
                return null;
            }
            ICommanderChannel? channel = null;
            foreach (ICommanderChannel c in commanders)
            {
                if (c.Player == slot - 1) channel = c;
            }
            if (channel is null)
            {
                notice = string.Format(CultureInfo.InvariantCulture, CommanderChatText.UnknownSlot, slotText);
                return null;
            }
            if (body.Length == 0)
            {
                notice = CommanderChatText.EmptyMessage;
                return null;
            }
            return (channel, body);
        }

        return (commanders[0], text);
    }

    /// <summary>
    /// The lines the panel draws now, oldest first. Closed: lines no older than <see cref="FadeTicks"/>. Typing: the last
    /// <see cref="TypingLineCount"/> lines whatever their age.
    /// </summary>
    public void VisibleLines(int tick, List<CommanderLine> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        if (Typing)
        {
            int from = Math.Max(0, _lines.Count - TypingLineCount);
            for (int i = from; i < _lines.Count; i++) into.Add(_lines[i]);
            return;
        }
        foreach (CommanderLine line in _lines)
        {
            if (tick - line.Tick <= FadeTicks) into.Add(line);
        }
    }

    /// <summary>
    /// Splits <paramref name="text"/> into rows no wider than <paramref name="maxWidth"/> as <paramref name="measure"/> sees them.
    /// Breaks at spaces; a word wider than a row is cut where it no longer fits. Always returns at least one row.
    /// </summary>
    public static IReadOnlyList<string> Wrap(string text, int maxWidth, Func<string, int> measure)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(measure);
        var rows = new List<string>();
        string line = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string rest = word;
            while (rest.Length > 0)
            {
                string candidate = line.Length == 0 ? rest : line + " " + rest;
                if (measure(candidate) <= maxWidth)
                {
                    line = candidate;
                    break;
                }
                if (line.Length > 0)
                {
                    rows.Add(line);
                    line = "";
                    continue;
                }
                int n = 1;
                while (n < rest.Length && measure(rest[..(n + 1)]) <= maxWidth) n++;
                rows.Add(rest[..n]);
                rest = rest[n..];
            }
        }
        if (line.Length > 0 || rows.Count == 0) rows.Add(line);
        return rows;
    }
}
