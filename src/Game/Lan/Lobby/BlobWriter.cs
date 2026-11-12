// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game.Lan.Lobby;

/// <summary>Little-endian writer for the lobby and setup blobs (M9-05). Strings are UTF-8 with a one-byte length, cut to fit.</summary>
internal sealed class BlobWriter
{
    private readonly MemoryStream _ms = new();

    public void U8(int v) => _ms.WriteByte((byte)v);

    public void Bool(bool v) => _ms.WriteByte(v ? (byte)1 : (byte)0);

    public void I32(int v)
    {
        Span<byte> b = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(b, v);
        _ms.Write(b);
    }

    public void U64(ulong v)
    {
        Span<byte> b = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(b, v);
        _ms.Write(b);
    }

    public void Str(string? s, int maxBytes)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
        int n = Math.Min(bytes.Length, maxBytes);
        // do not cut in the middle of a multi-byte character
        while (n > 0 && n < bytes.Length && (bytes[n] & 0xC0) == 0x80) n--;
        _ms.WriteByte((byte)n);
        _ms.Write(bytes, 0, n);
    }

    public byte[] ToArray() => _ms.ToArray();
}
