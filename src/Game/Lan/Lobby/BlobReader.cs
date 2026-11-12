// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Text;

namespace Rts.Game.Lan.Lobby;

/// <summary>Bounds-checked reader for the lobby and setup blobs (M9-05). Every problem is an <see cref="InvalidDataException"/>.</summary>
internal sealed class BlobReader
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private readonly byte[] _data;
    private int _at;

    public BlobReader(byte[] data)
    {
        _data = data ?? throw new InvalidDataException("No data.");
    }

    public int U8()
    {
        Need(1);
        return _data[_at++];
    }

    public bool Bool()
    {
        int v = U8();
        if (v > 1) throw new InvalidDataException("Bad flag.");
        return v == 1;
    }

    public int I32()
    {
        Need(4);
        int v = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_at, 4));
        _at += 4;
        return v;
    }

    public ulong U64()
    {
        Need(8);
        ulong v = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(_data.AsSpan(_at, 8));
        _at += 8;
        return v;
    }

    /// <summary>A byte in 0..<paramref name="max"/> inclusive.</summary>
    public int Ranged(int max, string what)
    {
        int v = U8();
        if (v > max) throw new InvalidDataException("Bad " + what + ".");
        return v;
    }

    public string Str(int maxBytes)
    {
        int n = U8();
        if (n > maxBytes) throw new InvalidDataException("Text too long.");
        Need(n);
        try { string s = Utf8.GetString(_data, _at, n); _at += n; return s; }
        catch (ArgumentException) { throw new InvalidDataException("Bad text."); }
    }

    public void End()
    {
        if (_at != _data.Length) throw new InvalidDataException("Extra bytes.");
    }

    private void Need(int n)
    {
        if (n < 0 || _data.Length - _at < n) throw new InvalidDataException("Truncated.");
    }
}
