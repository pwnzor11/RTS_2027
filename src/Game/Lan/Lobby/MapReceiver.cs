// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;

namespace Rts.Game.Lan.Lobby;

/// <summary>
/// Puts the <c>FileChunk</c> messages of a custom map back together (M9-06, Q-M9-k). Bounded: at most <see cref="MaxBytes"/> in all, chunks of
/// at most <see cref="NetCodec.MaxChunkBytes"/>. A chunk of a newer transfer id restarts the assembly. The caller checks the hash of the result.
/// </summary>
public sealed class MapReceiver
{
    /// <summary>Largest custom map the lobby moves, in bytes.</summary>
    public const int MaxBytes = 1_000_000;

    private int _transfer = -1;
    private byte[][]? _parts;
    private int _have;

    /// <summary>Chunks received of the current transfer.</summary>
    public int Received => _have;

    /// <summary>Chunks the current transfer has (0 before any).</summary>
    public int Total => _parts?.Length ?? 0;

    /// <summary>Takes a map chunk (kind 0). Returns the whole file when it is complete, else null. Throws <see cref="InvalidDataException"/> for a chunk that cannot be right.</summary>
    public byte[]? Accept(NetMessage.FileChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Kind != 0) return null;
        int max = (MaxBytes + NetCodec.MaxChunkBytes - 1) / NetCodec.MaxChunkBytes;
        if (chunk.Count < 1 || chunk.Count > max || chunk.Index < 0 || chunk.Index >= chunk.Count || chunk.Data.Length > NetCodec.MaxChunkBytes)
            throw new InvalidDataException("Bad map chunk.");
        if (chunk.TransferId != _transfer || _parts is null || _parts.Length != chunk.Count)
        {
            _transfer = chunk.TransferId;
            _parts = new byte[chunk.Count][];
            _have = 0;
        }

        if (_parts[chunk.Index] is null) { _parts[chunk.Index] = chunk.Data; _have++; }
        if (_have < _parts.Length) return null;

        int size = 0;
        foreach (byte[] p in _parts) size += p.Length;
        if (size > MaxBytes) throw new InvalidDataException("The map is too large.");
        var all = new byte[size];
        int at = 0;
        foreach (byte[] p in _parts) { p.CopyTo(all, at); at += p.Length; }
        _parts = null;
        _have = 0;
        return all;
    }
}
