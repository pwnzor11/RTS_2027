// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net;

/// <summary>
/// Binary encoding of <see cref="NetMessage"/> (M9 contract). Little-endian, one type byte first, strings as UTF-8 with a length
/// prefix, arrays with a count prefix. Decode rejects anything malformed with <see cref="InvalidDataException"/> (never another
/// exception type), so a bad packet can be dropped without touching the game. Package M9-01 fills the bodies.
/// </summary>
public static class NetCodec
{
    /// <summary>Encodes a message.</summary>
    public static byte[] Encode(NetMessage message) => throw new NotImplementedException("M9-01");

    /// <summary>Decodes one message; throws <see cref="InvalidDataException"/> on malformed input.</summary>
    public static NetMessage Decode(ReadOnlySpan<byte> data) => throw new NotImplementedException("M9-01");
}
