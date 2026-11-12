// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Buffers.Binary;
using System.Text;

namespace Rts.Net.Discovery;

/// <summary>Wire format of discovery probes and replies. Decoding never throws; it returns null for anything that is not ours.</summary>
internal static class DiscoveryPacket
{
    private static readonly byte[] ProbeMagic = "RTSQ"u8.ToArray();
    private static readonly byte[] ReplyMagic = "RTSR"u8.ToArray();
    private const int MaxName = 64;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] EncodeProbe()
    {
        byte[] id = Utf8.GetBytes(DiscoveryInfo.GameName);
        var b = new byte[4 + 1 + id.Length];
        ProbeMagic.CopyTo(b, 0);
        b[4] = (byte)id.Length;
        id.CopyTo(b, 5);
        return b;
    }

    public static bool IsProbe(ReadOnlySpan<byte> d)
    {
        if (d.Length < 5 || !d[..4].SequenceEqual(ProbeMagic)) return false;
        int n = d[4];
        if (d.Length != 5 + n) return false;
        try { return Utf8.GetString(d.Slice(5, n)) == DiscoveryInfo.GameName; }
        catch (DecoderFallbackException) { return false; }
    }

    public static byte[] EncodeReply(DiscoveryInfo i)
    {
        byte[] id = Utf8.GetBytes(i.GameId.Length > 32 ? i.GameId[..32] : i.GameId);
        byte[] name = Clip(i.LobbyName);
        var b = new byte[4 + 1 + id.Length + 4 + 1 + name.Length + 1 + 1 + 2 + 1];
        int p = 0;
        ReplyMagic.CopyTo(b, p); p += 4;
        b[p++] = (byte)id.Length; id.CopyTo(b, p); p += id.Length;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(p), i.Protocol); p += 4;
        b[p++] = (byte)name.Length; name.CopyTo(b, p); p += name.Length;
        b[p++] = (byte)Math.Clamp(i.Players, 0, 255);
        b[p++] = (byte)Math.Clamp(i.MaxPlayers, 0, 255);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(p), (ushort)Math.Clamp(i.GamePort, 0, 65535)); p += 2;
        b[p] = (byte)((i.Open ? 1 : 0) | (i.Started ? 2 : 0));
        return b;
    }

    public static DiscoveryInfo? DecodeReply(ReadOnlySpan<byte> d)
    {
        try
        {
            if (d.Length < 4 || !d[..4].SequenceEqual(ReplyMagic)) return null;
            int p = 4;
            int n = d[p++];
            string id = Utf8.GetString(d.Slice(p, n)); p += n;
            int protocol = BinaryPrimitives.ReadInt32LittleEndian(d.Slice(p, 4)); p += 4;
            n = d[p++];
            if (n > MaxName) return null;
            string name = Utf8.GetString(d.Slice(p, n)); p += n;
            int players = d[p++], max = d[p++];
            int port = BinaryPrimitives.ReadUInt16LittleEndian(d.Slice(p, 2)); p += 2;
            int flags = d[p++];
            if (p != d.Length || id != DiscoveryInfo.GameName || port == 0 || (flags & ~3) != 0) return null;
            return new DiscoveryInfo(id, protocol, name, players, max, port, (flags & 1) != 0, (flags & 2) != 0);
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException) { return null; }
    }

    private static byte[] Clip(string s)
    {
        int bytes = 0, chars = 0;
        foreach (Rune r in s.EnumerateRunes())
        {
            if (bytes + r.Utf8SequenceLength > MaxName) break;
            bytes += r.Utf8SequenceLength; chars += r.Utf16SequenceLength;
        }
        return Utf8.GetBytes(s[..chars]);
    }
}
