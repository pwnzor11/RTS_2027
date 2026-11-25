// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Ui;

/// <summary>
/// U-03: the short reason shown in a save-failure toast ("Quicksave failed: the save file could not be written"). The raw exception
/// text is not shown; the full exception goes to the log (see <c>GameSaveTarget.Save</c>).
/// </summary>
public static class SaveFailureText
{
    /// <summary>The short, player-facing reason for a failed save.</summary>
    /// <param name="error">The exception the write raised.</param>
    public static string Reason(Exception error) => error switch
    {
        UnauthorizedAccessException => "access to the save folder was denied",
        IOException => "the save file could not be written",
        _ => "an unexpected error",
    };
}
