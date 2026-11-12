// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;

namespace Rts.Game.Lan.Lobby;

/// <summary>Cuts a custom map file into <c>FileChunk</c> messages of kind 0 (M9-06).</summary>
public static class MapChunker
{
    /// <summary>The chunks of <paramref name="data"/> for transfer <paramref name="transferId"/>; empty when the file is empty or over <see cref="MapReceiver.MaxBytes"/>.</summary>
    public static IReadOnlyList<NetMessage.FileChunk> Split(byte[] data, int transferId)
    {
        if (data.Length == 0 || data.Length > MapReceiver.MaxBytes) return Array.Empty<NetMessage.FileChunk>();
        int count = (data.Length + NetCodec.MaxChunkBytes - 1) / NetCodec.MaxChunkBytes;
        var list = new List<NetMessage.FileChunk>(count);
        for (int i = 0; i < count; i++)
        {
            int from = i * NetCodec.MaxChunkBytes;
            int len = Math.Min(NetCodec.MaxChunkBytes, data.Length - from);
            list.Add(new NetMessage.FileChunk(0, transferId, i, count, data.AsSpan(from, len).ToArray()));
        }

        return list;
    }
}
