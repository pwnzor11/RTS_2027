// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.IO.Compression;
using Rts.Net;

namespace Rts.Game.Lan;

/// <summary>
/// The resync save transfer (M9-10, design 20 §4.5 and §9): the host's save of the paused tick, deflated, cut into
/// <see cref="NetMessage.FileChunk"/> pieces of kind <see cref="ChunkKind"/> that each fit one packet, and put back together on a client.
/// </summary>
internal sealed class LanResync
{
    /// <summary>FileChunk kind of a resync save (design 20 §3: 1 = resync save).</summary>
    public const byte ChunkKind = 1;

    /// <summary>Payload bytes per chunk: well under <see cref="NetProtocol.MaxPacketBytes"/> with the message and packet headers.</summary>
    public const int ChunkBytes = 900;

    private readonly Dictionary<int, byte[]> _chunks = new();
    private int _count = -1;

    public LanResync(int tick, int transferId)
    {
        Tick = tick;
        TransferId = transferId;
    }

    /// <summary>The tick the save is for: the client loads it right before stepping this tick.</summary>
    public int Tick { get; }

    /// <summary>The transfer id from ResyncBegin.</summary>
    public int TransferId { get; }

    /// <summary>True once every chunk has arrived.</summary>
    public bool Complete => _count >= 0 && _chunks.Count == _count;

    /// <summary>Takes one chunk; chunks of another transfer and duplicates are ignored.</summary>
    public void Add(NetMessage.FileChunk chunk)
    {
        if (chunk.TransferId != TransferId || chunk.Count <= 0 || chunk.Index < 0 || chunk.Index >= chunk.Count) return;
        _count = chunk.Count;
        _chunks.TryAdd(chunk.Index, chunk.Data);
    }

    /// <summary>The save bytes (inflated). Only when <see cref="Complete"/>.</summary>
    public byte[] Assemble()
    {
        if (!Complete) throw new InvalidOperationException("The resync save has not fully arrived.");
        using var packed = new MemoryStream();
        for (int i = 0; i < _count; i++) packed.Write(_chunks[i]);
        packed.Position = 0;
        using var inflate = new DeflateStream(packed, CompressionMode.Decompress);
        using var save = new MemoryStream();
        inflate.CopyTo(save);
        return save.ToArray();
    }

    /// <summary>Deflates a save and cuts it into chunks for transfer <paramref name="transferId"/>.</summary>
    public static List<NetMessage.FileChunk> Split(byte[] save, int transferId)
    {
        using var packed = new MemoryStream();
        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true)) deflate.Write(save);
        byte[] bytes = packed.ToArray();
        int count = Math.Max(1, (bytes.Length + ChunkBytes - 1) / ChunkBytes);
        var chunks = new List<NetMessage.FileChunk>(count);
        for (int i = 0; i < count; i++)
        {
            int start = i * ChunkBytes;
            int length = Math.Min(ChunkBytes, bytes.Length - start);
            chunks.Add(new NetMessage.FileChunk(ChunkKind, transferId, i, count, bytes.AsSpan(start, Math.Max(0, length)).ToArray()));
        }
        return chunks;
    }
}
