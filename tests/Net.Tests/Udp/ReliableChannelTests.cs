// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Buffers.Binary;
using Rts.Net;
using Rts.Net.Udp;

namespace Rts.Net.Tests.Udp;

public class ReliableChannelTests
{
    private const long Step = 1_000;   // 1 ms of fake time

    private static byte[] Msg(int i, int size = 8)
    {
        var b = new byte[Math.Max(size, 4)];
        BinaryPrimitives.WriteInt32LittleEndian(b, i);
        for (int k = 4; k < b.Length; k++) b[k] = (byte)(i + k);
        return b;
    }

    private static bool Valid(byte[] b, int i, int size)
    {
        if (b.Length != Math.Max(size, 4) || BinaryPrimitives.ReadInt32LittleEndian(b) != i) return false;
        for (int k = 4; k < b.Length; k++) if (b[k] != (byte)(i + k)) return false;
        return true;
    }

    private sealed class Pair
    {
        public readonly ReliableChannel A = new(0), B = new(0);
        public readonly FakeWire Wire;
        public long Now;
        private readonly byte[] _buf = new byte[NetProtocol.MaxPacketBytes];
        public readonly List<byte[]> AtA = new(), AtB = new();
        public int MaxDatagram;

        public Pair(FakeWire wire) { Wire = wire; }

        public void Tick()
        {
            Now += Step;
            foreach (var (to, data) in Wire.Deliver(Now))
            {
                ReliableChannel c = to == 0 ? A : B;
                Assert.True(c.OnPacket(Now, data));
                while (c.TryDequeueMessage(out byte[] m)) (to == 0 ? AtA : AtB).Add(m);
            }
            Pump(A, 1);
            Pump(B, 0);
        }

        private void Pump(ReliableChannel c, int to)
        {
            while (c.TryBuildPacket(Now, _buf, out int len))
            {
                MaxDatagram = Math.Max(MaxDatagram, len);
                Wire.Send(Now, to, _buf, len);
            }
        }
    }

    [Fact]
    public void CleanWireDeliversEverythingOnceInOrderWithNoResends()
    {
        var p = new Pair(new FakeWire(1, 0, 0, 5_000, 0));
        for (int i = 0; i < 300; i++) p.A.QueueReliable(Msg(i));
        for (int t = 0; t < 3000 && p.AtB.Count < 300; t++) p.Tick();
        Assert.Equal(300, p.AtB.Count);
        for (int i = 0; i < 300; i++) Assert.True(Valid(p.AtB[i], i, 8));
        Assert.Equal(0, p.A.Resends);
        Assert.True(p.MaxDatagram <= NetProtocol.MaxPacketBytes);
    }

    [Fact]
    public void SmallMessagesArePackedIntoFewDatagrams()
    {
        var p = new Pair(new FakeWire(1, 0, 0, 1_000, 0));
        for (int i = 0; i < 50; i++) p.A.QueueReliable(Msg(i, 20));
        p.Tick();
        // 50 x 25 bytes + header fits two datagrams at most.
        Assert.True(p.A.PacketsBuilt <= 2, "packets: " + p.A.PacketsBuilt);
    }

    [Theory]
    [InlineData(11, 0.20, 0.10, 20_000L, 30_000L)]
    [InlineData(12, 0.20, 0.00, 5_000L, 60_000L)]
    [InlineData(13, 0.35, 0.20, 10_000L, 40_000L)]
    public void LossDuplicationAndReorderingStillGiveExactlyOnceInOrder(int seed, double loss, double dup, long delay, long jitter)
    {
        var p = new Pair(new FakeWire(seed, loss, dup, delay, jitter));
        const int N = 1000;
        int sentA = 0, sentB = 0;
        for (int t = 0; t < 120_000 && (p.AtB.Count < N || p.AtA.Count < N); t++)
        {
            if (t % 5 == 0 && sentA < N) { p.A.QueueReliable(Msg(sentA, 4 + sentA % 40)); sentA++; }
            if (t % 7 == 0 && sentB < N) { p.B.QueueReliable(Msg(sentB, 4 + sentB % 30)); sentB++; }
            p.Tick();
        }
        Assert.Equal(N, p.AtB.Count);
        Assert.Equal(N, p.AtA.Count);
        for (int i = 0; i < N; i++)
        {
            Assert.True(Valid(p.AtB[i], i, 4 + i % 40), "A to B message " + i);
            Assert.True(Valid(p.AtA[i], i, 4 + i % 30), "B to A message " + i);
        }
        // Bounded resends: well under two extra transmissions per message even at this loss.
        Assert.True(p.A.Resends < N * 2, "A resends " + p.A.Resends);
        Assert.True(p.B.Resends < N * 2, "B resends " + p.B.Resends);
        Assert.True(p.Wire.Dropped > 0);
        Assert.True(p.MaxDatagram <= NetProtocol.MaxPacketBytes);
    }

    [Fact]
    public void LargeMessageIsFragmentedAndReassembledUnderLoss()
    {
        var p = new Pair(new FakeWire(21, 0.2, 0.1, 10_000, 20_000));
        var big = new byte[100_000];
        new Random(5).NextBytes(big);
        p.A.QueueReliable(Msg(1));
        p.A.QueueReliable(big);
        p.A.QueueReliable(Msg(2));
        for (int t = 0; t < 120_000 && p.AtB.Count < 3; t++) p.Tick();
        Assert.Equal(3, p.AtB.Count);
        Assert.True(Valid(p.AtB[0], 1, 8));
        Assert.Equal(big, p.AtB[1]);
        Assert.True(Valid(p.AtB[2], 2, 8));
        Assert.True(p.MaxDatagram <= NetProtocol.MaxPacketBytes);
    }

    [Fact]
    public void UnreliableMessagesAreNotResentAndNeverDuplicated()
    {
        var p = new Pair(new FakeWire(31, 0.3, 0.5, 5_000, 20_000));
        for (int i = 0; i < 400; i++)
        {
            p.A.QueueUnreliable(Msg(i));
            p.Tick(); p.Tick();
        }
        for (int t = 0; t < 200; t++) p.Tick();
        var seen = new HashSet<int>();
        foreach (byte[] m in p.AtB) Assert.True(seen.Add(BinaryPrimitives.ReadInt32LittleEndian(m)), "duplicate unreliable message");
        Assert.InRange(seen.Count, 100, 399);
        Assert.Equal(0, p.A.Resends);
    }

    [Fact]
    public void RoundTripTimeIsMeasuredAndResendTimeoutStaysInBounds()
    {
        var p = new Pair(new FakeWire(41, 0, 0, 20_000, 0));
        for (int i = 0; i < 20; i++) { p.A.QueueReliable(Msg(i)); p.B.QueueReliable(Msg(i)); for (int k = 0; k < 10; k++) p.Tick(); }
        Assert.InRange(p.A.SmoothedRttMicros, 38_000, 60_000);
        Assert.InRange(p.A.RetransmitTimeoutMicros, 50_000, 1_000_000);
    }

    [Fact]
    public void IdleChannelSendsKeepalivesAndGarbageIsRejected()
    {
        var c = new ReliableChannel(0);
        var buf = new byte[NetProtocol.MaxPacketBytes];
        Assert.False(c.TryBuildPacket(100_000, buf, out _));
        Assert.True(c.TryBuildPacket(ReliableChannel.KeepaliveMicros, buf, out int len));
        Assert.Equal(ReliableChannel.HeaderBytes, len);
        Assert.False(c.OnPacket(0, new byte[] { 1, 2, 3 }));
        Assert.False(c.OnPacket(0, new byte[20]));
        byte[] bad = (byte[])buf.AsSpan(0, len).ToArray().Concat(new byte[] { 0x80, 0, 0 }).ToArray();
        Assert.False(c.OnPacket(0, bad));
        var rng = new Random(9);
        for (int i = 0; i < 2000; i++)
        {
            var junk = new byte[rng.Next(0, 100)];
            rng.NextBytes(junk);
            if (junk.Length > 0) junk[0] = ReliableChannel.Magic;
            c.OnPacket(0, junk);   // must never throw
            while (c.TryDequeueMessage(out _)) { }
        }
    }

    [Fact]
    public void OversizedReassemblyFaultsTheChannel()
    {
        // A hostile peer streams fragments (more-flag set) forever.
        var victim = new ReliableChannel(0);
        var buf = new byte[NetProtocol.MaxPacketBytes];
        ushort seq = 0;
        for (int packet = 1; packet < 2000 && !victim.Faulted; packet++)
        {
            int pos = ReliableChannel.HeaderBytes;
            buf[0] = ReliableChannel.Magic;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(1), (ushort)packet);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(3), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(5), 0);
            buf[pos++] = 3;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(pos), seq++); pos += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(pos), (ushort)ReliableChannel.MaxUnitBytes); pos += 2;
            pos += ReliableChannel.MaxUnitBytes;
            victim.OnPacket(0, buf.AsSpan(0, pos));
        }
        Assert.True(victim.Faulted);
    }
}
