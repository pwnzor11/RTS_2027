// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Llm;

// M5 wave contracts (session 7, lead-owned): what the commander loop (M5-05), personas (M5-06), chat panel (M5-07) and setup (M5-09) share.

/// <summary>A leader persona for an LLM commander (M5-06, from docs/lore/leaders.md). <see cref="Voice"/> is the persona part of the
/// system prompt (who you are, how you talk; no game rules, no secrets from the lore's "Secret" paragraphs).</summary>
public sealed record CommanderPersona(string Id, string Faction, string DisplayName, string Voice)
{
    /// <summary>Used when no persona file matches: a plain, unnamed commander.</summary>
    public static CommanderPersona Plain(string faction) =>
        new("plain_" + faction.ToLowerInvariant(), faction, "Commander", "You are a calm, terse field commander. Speak in short sentences.");
}

/// <summary>One line in the commander chat (M5-07 shows these). <see cref="FromPlayer"/> = typed by the local player
/// (<see cref="Player"/> is then the addressed commander's slot). <see cref="System"/> = a notice such as "commander offline".</summary>
public sealed record CommanderLine(int Tick, byte Player, string Speaker, string Text, bool FromPlayer = false, bool System = false);

/// <summary>Thread-safe chat log shared by the commanders (writers, any thread) and the chat panel (reader, game thread).
/// Lines are trimmed to <see cref="MaxLineChars"/>; at most <see cref="Capacity"/> lines are kept (oldest dropped).</summary>
public sealed class CommanderChatLog
{
    /// <summary>Longest line kept (design §5: ~200 chars).</summary>
    public const int MaxLineChars = 200;

    /// <summary>Lines kept.</summary>
    public const int Capacity = 200;

    private readonly object _gate = new();
    private readonly List<CommanderLine> _lines = new();
    private int _version;

    /// <summary>Adds a line (text trimmed and shortened; an empty line is ignored).</summary>
    public void Add(CommanderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        string text = (line.Text ?? "").Trim().ReplaceLineEndings(" ");
        if (text.Length == 0) return;
        if (text.Length > MaxLineChars) text = text[..(MaxLineChars - 1)] + "…";
        lock (_gate)
        {
            _lines.Add(line with { Text = text });
            if (_lines.Count > Capacity) _lines.RemoveAt(0);
            _version++;
        }
    }

    /// <summary>Increases on every Add, so a reader can skip copying when nothing changed.</summary>
    public int Version { get { lock (_gate) return _version; } }

    /// <summary>Copies the lines, oldest first, into <paramref name="into"/> (cleared first).</summary>
    public void CopyTo(List<CommanderLine> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        lock (_gate) { into.Clear(); into.AddRange(_lines); }
    }
}

/// <summary>What the chat panel (M5-07) needs from one LLM commander (M5-05 implements it).</summary>
public interface ICommanderChannel
{
    /// <summary>The commander's sim slot.</summary>
    byte Player { get; }

    /// <summary>Speaker name shown in the chat, e.g. "Brigadier Hester Wrenfield".</summary>
    string Speaker { get; }

    /// <summary>False when the commander is not calling the model (no key, failed provider, call cap reached): the slot plays its plain tier.</summary>
    bool Online { get; }

    /// <summary>The local player typed <paramref name="text"/> to this commander at <paramref name="tick"/>. Flavour only (Q-M5-e): it may
    /// produce a reply line in the log but never changes the directive. Never blocks.</summary>
    void Hear(int tick, string text);
}
