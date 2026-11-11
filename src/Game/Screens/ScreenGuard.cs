// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Data;
using Rts.Game.Saves;

namespace Rts.Game.Screens;

/// <summary>
/// One place that turns "a data file is damaged or missing" into a Notice instead of a crash (S18 GS-10). Screen factories that read files
/// (a campaign, a briefing, a saved game, a map) run through <see cref="TryCreate"/> or <see cref="Push"/>; the exceptions caught are the
/// ones the loaders throw for bad files, never programming errors such as a null reference.
/// </summary>
public static class ScreenGuard
{
    /// <summary>True for an exception that means a data file could not be read or does not fit the game.</summary>
    public static bool IsDataError(Exception e) =>
        e is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException
            or FormatException or KeyNotFoundException or IndexOutOfRangeException or NotSupportedException
            or SaveLoadException or MapFileException or RulesException;

    /// <summary>The text a Notice shows for a data error: the exception's own message.</summary>
    public static string Describe(Exception e) => string.IsNullOrWhiteSpace(e.Message) ? "The game data could not be read." : e.Message;

    /// <summary>
    /// Runs <paramref name="factory"/>. When it throws a data error, pushes a Notice titled <paramref name="title"/> with the reason and
    /// returns null; otherwise returns the screen (not pushed).
    /// </summary>
    public static IScreen? TryCreate(GameServices s, string title, Func<IScreen> factory)
    {
        try
        {
            return factory();
        }
        catch (Exception e) when (IsDataError(e))
        {
            s.Screens.Push(new NoticeScreen(s, title, Describe(e)));
            return null;
        }
    }

    /// <summary>Builds a screen with <see cref="TryCreate"/> and pushes it when it was built.</summary>
    public static void Push(GameServices s, string title, Func<IScreen> factory)
    {
        if (TryCreate(s, title, factory) is { } screen) s.Screens.Push(screen);
    }
}
