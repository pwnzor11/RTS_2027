// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Screens;

/// <summary>Short strings of the LLM commander option on the skirmish setup screen and the spectator table (M5-09).</summary>
public static class SetupText
{
    /// <summary>Shown before the tier name of an AI slot led by the LLM commander ("LLM · Veteran" in the design; the bitmap font is ASCII).</summary>
    public const string CommanderLabel = "LLM -";

    /// <summary>Shown after a Commander slot's row when no provider or key is set.</summary>
    public const string NoKeyHint = "(no key - plays as standard AI)";

    /// <summary>Title of the notice shown when an LLM commander is picked without a key.</summary>
    public const string NoKeyNoticeTitle = "LLM commander";

    /// <summary>Text of the notice shown when an LLM commander is picked without a key.</summary>
    public const string NoKeyNotice = "No LLM key is set, so this slot will play as the standard AI. Add a key in Settings to use the commander.";

    /// <summary>Marker next to a spectator row's player name.</summary>
    public const string SpectatorTag = "LLM";
}
