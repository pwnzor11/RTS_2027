// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Net;
using Rts.Sim.Core;

namespace Rts.Net.Tests.Codec;

public class NetCodecTests
{
    private static Command Cmd(int i) => new(100 + i, (byte)(i % 8), (ushort)(i * 7), (CommandKind)(i % 28),
        new EntityId((ushort)(i + 1), (ushort)(i * 3)), new EntityId((ushort)(i + 2), 5), new CellCoord(-i, i * 1000), int.MinValue + i, int.MaxValue - i);

    public static IEnumerable<object[]> Samples()
    {
        NetMessage[] all =
        {
            new NetMessage.Hello(1, "0.9.1", ulong.MaxValue - 3, "Pat é中", true),
            new NetMessage.Hello(1, "", 0, "", false),
            new NetMessage.Welcome(3),
            new NetMessage.Reject("Lobby full"),
            new NetMessage.LobbyState(new byte[] { 1, 2, 3 }),
            new NetMessage.LobbyState(Array.Empty<byte>()),
            new NetMessage.SlotRequest(new byte[] { 9 }),
            new NetMessage.Ready(true),
            new NetMessage.Ready(false),
            new NetMessage.Start(3, new byte[] { 4, 5, 6, 7 }),
            new NetMessage.Input(new[] { Cmd(1), Cmd(2) }),
            new NetMessage.Input(Array.Empty<Command>()),
            new NetMessage.TurnBundle(12345, new[] { Cmd(3) }),
            new NetMessage.TurnBundle(0, Array.Empty<Command>()),
            new NetMessage.Progress(-1),
            new NetMessage.Hash(600, 0xDEADBEEFCAFEF00DUL, new ulong[] { 1, 2, 3 }),
            new NetMessage.Hash(0, 0, Array.Empty<ulong>()),
            new NetMessage.Chat(2, 1, "hello team"),
            new NetMessage.Pause(1),
            new NetMessage.Resume(1, 3000),
            new NetMessage.Waiting(new[] { 1, 2 }, 1500),
            new NetMessage.Waiting(Array.Empty<int>(), 0),
            new NetMessage.PeerDropped(2, 900, "timeout"),
            new NetMessage.Desync(1200, new[] { 1 }),
            new NetMessage.ResyncBegin(1200, 7),
            new NetMessage.FileChunk(1, 7, 2, 5, new byte[] { 1, 2, 3 }),
            new NetMessage.Ping(long.MinValue),
            new NetMessage.Pong(long.MaxValue),
            new NetMessage.Bye("bye"),
        };
        foreach (NetMessage m in all) yield return new object[] { m };
    }

    private static void AssertSame(NetMessage expected, NetMessage actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());
        switch (expected)
        {
            case NetMessage.LobbyState e: Assert.Equal(e.Blob, ((NetMessage.LobbyState)actual).Blob); break;
            case NetMessage.SlotRequest e: Assert.Equal(e.Blob, ((NetMessage.SlotRequest)actual).Blob); break;
            case NetMessage.Start e: { var a = (NetMessage.Start)actual; Assert.Equal(e.CommandDelay, a.CommandDelay); Assert.Equal(e.SetupBlob, a.SetupBlob); break; }
            case NetMessage.Input e: Assert.Equal(e.Commands, ((NetMessage.Input)actual).Commands); break;
            case NetMessage.TurnBundle e: { var a = (NetMessage.TurnBundle)actual; Assert.Equal(e.Tick, a.Tick); Assert.Equal(e.Commands, a.Commands); break; }
            case NetMessage.Hash e: { var a = (NetMessage.Hash)actual; Assert.Equal(e.Tick, a.Tick); Assert.Equal(e.Value, a.Value); Assert.Equal(e.SubHashes, a.SubHashes); break; }
            case NetMessage.Waiting e: { var a = (NetMessage.Waiting)actual; Assert.Equal(e.Peers, a.Peers); Assert.Equal(e.WaitedMs, a.WaitedMs); break; }
            case NetMessage.Desync e: { var a = (NetMessage.Desync)actual; Assert.Equal(e.Tick, a.Tick); Assert.Equal(e.Peers, a.Peers); break; }
            case NetMessage.FileChunk e:
            {
                var a = (NetMessage.FileChunk)actual;
                Assert.Equal((e.Kind, e.TransferId, e.Index, e.Count), (a.Kind, a.TransferId, a.Index, a.Count));
                Assert.Equal(e.Data, a.Data);
                break;
            }
            default: Assert.Equal(expected, actual); break;
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EveryMessageRoundTrips(NetMessage message)
    {
        byte[] bytes = NetCodec.Encode(message);
        AssertSame(message, NetCodec.Decode(bytes));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void EveryTruncationIsRejectedCleanly(NetMessage message)
    {
        byte[] bytes = NetCodec.Encode(message);
        for (int len = 0; len < bytes.Length; len++)
        {
            byte[] cut = bytes[..len];
            Assert.Throws<InvalidDataException>(() => NetCodec.Decode(cut));
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void TrailingBytesAreRejected(NetMessage message)
    {
        byte[] bytes = NetCodec.Encode(message).Append((byte)0).ToArray();
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(bytes));
    }

    [Fact]
    public void UnknownTypeByteIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(new byte[] { 0 }));
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(new byte[] { 200, 1, 2, 3 }));
    }

    [Fact]
    public void OversizedCountsAreRejectedWithoutAllocating()
    {
        // TurnBundle claiming 65535 commands with no bytes behind it, and one claiming more than the cap.
        byte[] huge = { 9, 0, 0, 0, 0, 0xFF, 0xFF };
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(huge));
        // LobbyState claiming a 2 GB blob.
        byte[] blob = { 4, 0xFF, 0xFF, 0xFF, 0x7F };
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(blob));
        byte[] negative = { 4, 0xFF, 0xFF, 0xFF, 0xFF };
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(negative));
        // Hash with 65535 sub-hashes.
        byte[] hash = new byte[1 + 4 + 8 + 2];
        hash[0] = 11; hash[13] = 0xFF; hash[14] = 0xFF;
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(hash));
    }

    [Fact]
    public void BadBooleanAndBadUtf8AreRejected()
    {
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(new byte[] { 6, 2 }));
        // Reject with a 2-byte string that is invalid UTF-8.
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(new byte[] { 3, 2, 0, 0xC3, 0x28 }));
        // String longer than the 512-byte cap.
        var longStr = new byte[1 + 2 + 513];
        longStr[0] = 3; longStr[1] = 0x01; longStr[2] = 0x02;
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(longStr));
    }

    [Fact]
    public void LongStringsAreClippedOnEncodeAndChatIsCappedAt200Chars()
    {
        var m = (NetMessage.Reject)NetCodec.Decode(NetCodec.Encode(new NetMessage.Reject(new string('x', 2000))));
        Assert.Equal(512, m.Reason.Length);
        // Multi-byte text is cut on a character boundary and stays valid.
        var m2 = (NetMessage.Reject)NetCodec.Decode(NetCodec.Encode(new NetMessage.Reject(new string('中', 1000))));
        Assert.Equal(170, m2.Reason.Length);
        var chat = (NetMessage.Chat)NetCodec.Decode(NetCodec.Encode(new NetMessage.Chat(1, 0, new string('a', 500))));
        Assert.Equal(200, chat.Text.Length);
    }

    [Fact]
    public void ChatOver200CharsOnTheWireIsRejected()
    {
        var w = new List<byte> { 12, 1, 0, 0, 0, 0 };
        w.Add(201); w.Add(0);
        w.AddRange(Enumerable.Repeat((byte)'a', 201));
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(w.ToArray()));
    }

    [Fact]
    public void ArraysOverTheCapThrowOnEncode()
    {
        Assert.Throws<ArgumentException>(() => NetCodec.Encode(new NetMessage.Input(new Command[NetCodec.MaxCommands + 1])));
        Assert.Throws<ArgumentException>(() => NetCodec.Encode(new NetMessage.LobbyState(new byte[NetCodec.MaxBlobBytes + 1])));
        Assert.Throws<ArgumentException>(() => NetCodec.Encode(new NetMessage.Hash(0, 0, new ulong[NetCodec.MaxSubHashes + 1])));
        Assert.Throws<ArgumentException>(() => NetCodec.Encode(new NetMessage.FileChunk(0, 0, 0, 1, new byte[NetCodec.MaxChunkBytes + 1])));
    }

    [Fact]
    public void BadChunkIndexIsRejected()
    {
        byte[] bytes = NetCodec.Encode(new NetMessage.FileChunk(0, 1, 0, 3, new byte[2]));
        bytes[1 + 1 + 4 + 0] = 3;   // Index = 3 (>= Count 3)
        Assert.Throws<InvalidDataException>(() => NetCodec.Decode(bytes));
    }

    [Fact]
    public void RandomBytesOnlyEverThrowInvalidData()
    {
        var rng = new Random(20261112);
        int decoded = 0;
        for (int i = 0; i < 20000; i++)
        {
            var buf = new byte[rng.Next(0, 80)];
            rng.NextBytes(buf);
            if (buf.Length > 0 && rng.Next(2) == 0) buf[0] = (byte)rng.Next(0, 24);   // aim at real type bytes
            try { NetCodec.Decode(buf); decoded++; }
            catch (InvalidDataException) { }
        }
        Assert.True(decoded >= 0);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void MutatedValidMessagesOnlyEverThrowInvalidData(NetMessage message)
    {
        byte[] original = NetCodec.Encode(message);
        var rng = new Random(77);
        for (int i = 0; i < 300; i++)
        {
            byte[] copy = (byte[])original.Clone();
            for (int k = 0; k < 1 + rng.Next(3) && copy.Length > 0; k++) copy[rng.Next(copy.Length)] = (byte)rng.Next(256);
            try { NetCodec.Decode(copy); }
            catch (InvalidDataException) { }
        }
    }
}
