// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Saves;

/// <summary>A save could not be read or started. <see cref="Exception.Message"/> is written for the player and is shown as it is.</summary>
public sealed class SaveLoadException : Exception
{
    /// <summary>Creates the exception with a player-facing message.</summary>
    public SaveLoadException(string message) : base(message)
    {
    }

    /// <summary>Creates the exception with a player-facing message and its cause.</summary>
    public SaveLoadException(string message, Exception inner) : base(message, inner)
    {
    }
}
