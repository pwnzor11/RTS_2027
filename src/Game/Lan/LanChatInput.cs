// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Lan;

/// <summary>
/// The one-line LAN chat input (M9-08, Q-M9-g): Enter opens it, Tab toggles All / Team, Enter sends, Esc cancels. Graphics-free; the
/// overlay feeds it keys and draws it. Only printable ASCII is kept (the bitmap font is ASCII).
/// </summary>
public sealed class LanChatInput
{
    private readonly System.Text.StringBuilder _text = new();

    /// <summary>Longest line the input takes (the host cuts relayed lines to the same length).</summary>
    public const int MaxChars = Rts.Net.Lockstep.LockstepHost.MaxChatChars;

    /// <summary>True while the line is open.</summary>
    public bool Typing { get; private set; }

    /// <summary>True when the line goes to the sender's team only.</summary>
    public bool Team { get; private set; }

    /// <summary>The text typed so far.</summary>
    public string Text => _text.ToString();

    /// <summary>Opens the line (to everyone, empty).</summary>
    public void Open()
    {
        Typing = true;
        Team = false;
        _text.Clear();
    }

    /// <summary>Closes the line without sending.</summary>
    public void Cancel()
    {
        Typing = false;
        _text.Clear();
    }

    /// <summary>Switches between All and Team.</summary>
    public void ToggleAudience() => Team = !Team;

    /// <summary>Adds one character (control characters and non-ASCII are ignored).</summary>
    public void Type(char ch)
    {
        if (!Typing || ch < ' ' || ch > '~' || _text.Length >= MaxChars) return;
        _text.Append(ch);
    }

    /// <summary>Removes the last character.</summary>
    public void Backspace()
    {
        if (_text.Length > 0) _text.Length--;
    }

    /// <summary>Closes the line and returns what to send, or null when it was empty.</summary>
    public (bool Team, string Text)? Submit()
    {
        string text = _text.ToString().Trim();
        bool team = Team;
        Cancel();
        return text.Length == 0 ? null : (team, text);
    }
}
