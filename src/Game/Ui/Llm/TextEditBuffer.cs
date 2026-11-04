// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game.Ui.Llm;

/// <summary>A one-line text edit buffer for the LLM settings text boxes. Control characters are dropped, length is capped.
/// <see cref="ToString"/> shows only the length so a secret in it cannot leak into logs.</summary>
public sealed class TextEditBuffer
{
    private readonly StringBuilder _text = new();
    private readonly int _max;

    /// <summary>Creates a buffer holding <paramref name="initial"/> (clipped), at most <paramref name="maxLength"/> characters.</summary>
    public TextEditBuffer(string initial, int maxLength)
    {
        _max = Math.Max(1, maxLength);
        Append(initial);
    }

    /// <summary>The current text.</summary>
    public string Text => _text.ToString();

    /// <summary>Number of characters.</summary>
    public int Length => _text.Length;

    /// <summary>Adds the printable characters of <paramref name="s"/> (a typed character or pasted text); returns how many were added.</summary>
    public int Append(string? s)
    {
        int added = 0;
        if (s is null) return 0;
        foreach (char c in s)
        {
            if (char.IsControl(c) || _text.Length >= _max) continue;
            _text.Append(c);
            added++;
        }
        return added;
    }

    /// <summary>Removes the last character, if any.</summary>
    public void Backspace()
    {
        if (_text.Length > 0) _text.Length--;
    }

    /// <summary>Empties the buffer.</summary>
    public void Clear() => _text.Clear();

    /// <summary>One '*' per character, for masked boxes.</summary>
    public string Masked() => new('*', _text.Length);

    /// <summary>Length only, never the text.</summary>
    public override string ToString() => $"TextEditBuffer({_text.Length} chars)";
}
