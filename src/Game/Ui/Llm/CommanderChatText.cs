// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui.Llm;

/// <summary>Player-visible text of the commander chat panel (M5-07). Plain and short.</summary>
public static class CommanderChatText
{
    /// <summary>Shown under the input line while typing.</summary>
    public const string InputHint = "Enter send   Esc cancel   /to 2 text: pick commander 2";

    /// <summary>Shown in the closed panel when there is no chat yet.</summary>
    public const string OpenHint = "Enter: talk to your commander";

    /// <summary>Prefix of a line the player typed (followed by the commander's name).</summary>
    public const string YouTo = "You -> ";   // ASCII: the UI font has no arrow glyph

    /// <summary>Input prompt.</summary>
    public const string Prompt = "> ";

    /// <summary>Shown when the typed text has no commander to go to.</summary>
    public const string NoCommander = "No commander in this game.";

    /// <summary>Shown when "/to" names a slot with no commander. The argument is the slot as typed.</summary>
    public const string UnknownSlot = "No commander in slot {0}.";

    /// <summary>Shown when "/to" has no text after the slot.</summary>
    public const string EmptyMessage = "Type a message after the slot number.";
}
