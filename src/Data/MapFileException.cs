// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Data;

/// <summary>Thrown when a map file cannot be read or fails validation.</summary>
public sealed class MapFileException : Exception
{
    public MapFileException(string path, string message)
        : base($"{path}: {message}")
    {
        Path = path;
    }

    public MapFileException(string path, string message, Exception inner)
        : base($"{path}: {message}", inner)
    {
        Path = path;
    }

    /// <summary>The file that failed.</summary>
    public string Path { get; }
}
