// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Buffers.Binary;

namespace Rts.Net.Udp;

/// <summary>
/// The reliability state machine for ONE peer, with no sockets and no clock of its own (every call takes the caller's monotonic
/// microseconds). It turns byte messages into datagrams and back: packet ids, an ack plus 32-bit ack bitfield on every packet,
/// resend of unacked reliable units after RTT x 1.5 (min 50 ms, max 1 s), duplicate drop, reliable messages delivered exactly once
/// in send order, several small messages packed into one datagram (never more than <see cref="NetProtocol.MaxPacketBytes"/>), a large
/// reliable message split into fragments and reassembled, a keepalive when idle, and a smoothed RTT.
/// Datagram layout: magic(1) packetId(u16) ack(u16) ackBits(u32), then items. Item = flags(1) [reliableSeq(u16) if reliable] len(u16) bytes.
/// Flags: bit0 reliable, bit1 more fragments follow. Unreliable messages are delivered at most once and are not resent.
/// </summary>
public sealed class ReliableChannel
{
    /// <summary>First byte of every datagram; anything else is not ours.</summary>
    public const byte Magic = 0x52;

    /// <summary>Datagram header size in bytes.</summary>
    public const int HeaderBytes = 9;

    /// <summary>Per-item overhead of a reliable unit (flags, sequence, length).</summary>
    public const int ReliableOverhead = 5;

    /// <summary>Largest message that fits one reliable unit; longer ones are fragmented.</summary>
    public const int MaxUnitBytes = NetProtocol.MaxPacketBytes - HeaderBytes - ReliableOverhead;

    /// <summary>Largest reassembled message accepted (bigger means a faulty or hostile peer).</summary>
    public const int MaxMessageBytes = 512 * 1024;

    /// <summary>Most unacknowledged reliable units in flight; further ones wait in the queue.</summary>
    public const int SendWindow = 256;

    /// <summary>Receive window (units ahead of the next expected one that are buffered).</summary>
    public const int ReceiveWindow = 512;

    /// <summary>An idle channel sends an empty packet this often.</summary>
    public const long KeepaliveMicros = 250_000;

    private const long MinRtoMicros = 50_000, MaxRtoMicros = 1_000_000, InitialRttMicros = 100_000;
    private const int SentRing = 128;

    private sealed class Unit
    {
        public ushort Seq;
        public bool More;
        public byte[] Data = Array.Empty<byte>();
        public long LastSent = -1;
        public bool Acked;
    }

    private sealed class SentPacket
    {
        public int Id = -1;
        public long SentAt;
        public readonly List<Unit> Units = new();
    }

    // Send side.
    private readonly List<Unit> _units = new();
    private readonly Queue<byte[]> _unreliable = new();
    private readonly SentPacket[] _sent = new SentPacket[SentRing];
    private ushort _nextSeq;
    private ushort _nextPacketId = 1;   // 0 means "nothing received yet" in the ack field, so it is never used
    private long _lastSendAt;

    // Receive side.
    private readonly Dictionary<ushort, (bool More, byte[] Data)> _recvBuf = new();
    private readonly Queue<byte[]> _messages = new();
    private readonly List<byte> _assembling = new();
    private ushort _recvNext;
    private bool _hasRecv;
    private ushort _recvHigh;
    private uint _recvBits;
    private bool _ackDirty;
    private long _lastRecvAt;
    private long _srtt = -1;

    /// <summary>Creates a channel whose silence timer starts at <paramref name="nowMicroseconds"/>.</summary>
    public ReliableChannel(long nowMicroseconds)
    {
        _lastRecvAt = nowMicroseconds;
        _lastSendAt = nowMicroseconds;
        for (int i = 0; i < SentRing; i++) _sent[i] = new SentPacket();
    }

    /// <summary>Restarts the silence and keepalive timers at <paramref name="nowMicroseconds"/> (used when the clock only becomes known later).</summary>
    public void Restart(long nowMicroseconds)
    {
        _lastRecvAt = nowMicroseconds;
        _lastSendAt = nowMicroseconds;
    }

    /// <summary>Set when the peer broke the protocol (oversized reassembly); the owner should drop the peer.</summary>
    public bool Faulted { get; private set; }

    /// <summary>Number of reliable unit transmissions beyond the first (for tests and diagnostics).</summary>
    public int Resends { get; private set; }

    /// <summary>Number of datagrams produced.</summary>
    public int PacketsBuilt { get; private set; }

    /// <summary>Smoothed round-trip time in microseconds (0 until the first sample).</summary>
    public long SmoothedRttMicros => _srtt < 0 ? 0 : _srtt;

    /// <summary>When the last valid datagram arrived.</summary>
    public long LastReceivedMicros => _lastRecvAt;

    /// <summary>True when every reliable unit queued so far has been acknowledged.</summary>
    public bool AllAcknowledged => _units.Count == 0;

    /// <summary>Current resend timeout in microseconds.</summary>
    public long RetransmitTimeoutMicros => Math.Clamp((_srtt < 0 ? InitialRttMicros : _srtt) * 3 / 2, MinRtoMicros, MaxRtoMicros);

    /// <summary>Queues a message for exactly-once, in-order delivery. A long message is fragmented.</summary>
    public void QueueReliable(byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length > MaxMessageBytes) throw new ArgumentException("Message too large.", nameof(message));
        int offset = 0;
        do
        {
            int n = Math.Min(MaxUnitBytes, message.Length - offset);
            bool more = offset + n < message.Length;
            _units.Add(new Unit { Seq = _nextSeq++, More = more, Data = message.AsSpan(offset, n).ToArray() });
            offset += n;
        } while (offset < message.Length);
    }

    /// <summary>Queues a message that may be lost. Must fit one unit (the caller sends longer ones as reliable).</summary>
    public void QueueUnreliable(byte[] message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length > MaxUnitBytes) throw new ArgumentException("Unreliable message too large.", nameof(message));
        if (_unreliable.Count >= 256) _unreliable.Dequeue();   // newest wins; old progress is stale anyway
        _unreliable.Enqueue(message);
    }

    /// <summary>
    /// Builds the next datagram into <paramref name="buffer"/> (at least <see cref="NetProtocol.MaxPacketBytes"/> long), or returns
    /// false when there is nothing worth sending now (no payload, no pending ack, keepalive not due).
    /// </summary>
    public bool TryBuildPacket(long now, byte[] buffer, out int length)
    {
        length = 0;
        long rto = RetransmitTimeoutMicros;
        int pos = HeaderBytes;
        var packetUnits = new List<Unit>();
        int windowLeft = SendWindow;
        for (int i = 0; i < _units.Count && windowLeft > 0; i++)
        {
            Unit u = _units[i];
            if (u.Acked) continue;
            windowLeft--;
            bool due = u.LastSent < 0 || now - u.LastSent >= rto;
            if (!due) continue;
            int need = ReliableOverhead + u.Data.Length;
            if (pos + need > NetProtocol.MaxPacketBytes) break;
            buffer[pos++] = (byte)(1 | (u.More ? 2 : 0));
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(pos), u.Seq); pos += 2;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(pos), (ushort)u.Data.Length); pos += 2;
            u.Data.CopyTo(buffer, pos); pos += u.Data.Length;
            if (u.LastSent >= 0) Resends++;
            u.LastSent = now;
            packetUnits.Add(u);
        }
        bool hasReliable = packetUnits.Count > 0;
        while (_unreliable.Count > 0 && pos + 3 + _unreliable.Peek().Length <= NetProtocol.MaxPacketBytes)
        {
            byte[] m = _unreliable.Dequeue();
            buffer[pos++] = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(pos), (ushort)m.Length); pos += 2;
            m.CopyTo(buffer, pos); pos += m.Length;
            hasReliable = true;   // payload present
        }
        bool keepaliveDue = now - _lastSendAt >= KeepaliveMicros;
        if (!hasReliable && !_ackDirty && !keepaliveDue) return false;

        ushort id = _nextPacketId++;
        if (_nextPacketId == 0) _nextPacketId = 1;
        buffer[0] = Magic;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1), id);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(3), _recvHigh);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(5), _recvBits);
        SentPacket sp = _sent[id % SentRing];
        sp.Id = id; sp.SentAt = now; sp.Units.Clear(); sp.Units.AddRange(packetUnits);
        _ackDirty = false;
        _lastSendAt = now;
        PacketsBuilt++;
        length = pos;
        return true;
    }

    /// <summary>
    /// Processes one received datagram. Returns false (and changes nothing) when it is not a valid datagram of this protocol;
    /// the owner just drops it. Complete messages become available through <see cref="TryDequeueMessage"/>.
    /// </summary>
    public bool OnPacket(long now, ReadOnlySpan<byte> packet)
    {
        if (packet.Length < HeaderBytes || packet[0] != Magic || Faulted) return false;
        // Validate the item list first so a half-bad packet changes nothing.
        int p = HeaderBytes;
        while (p < packet.Length)
        {
            byte flags = packet[p++];
            if ((flags & ~3) != 0) return false;
            if ((flags & 1) != 0) { if (p + 2 > packet.Length) return false; p += 2; }
            else if ((flags & 2) != 0) return false;
            if (p + 2 > packet.Length) return false;
            int len = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(p, 2)); p += 2;
            if (len > MaxUnitBytes || p + len > packet.Length) return false;
            p += len;
        }

        ushort id = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(1, 2));
        ushort ack = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(3, 2));
        uint ackBits = BinaryPrimitives.ReadUInt32LittleEndian(packet.Slice(5, 4));
        _lastRecvAt = now;
        ProcessAcks(now, ack, ackBits);

        // Packet-level duplicate and staleness check.
        bool fresh = true;
        if (!_hasRecv) { _hasRecv = true; _recvHigh = id; _recvBits = 0; }
        else
        {
            int diff = (short)(ushort)(id - _recvHigh);
            if (diff > 0)
            {
                _recvBits = diff >= 33 ? 0 : (diff == 32 ? 0 : _recvBits << diff);
                if (diff <= 32) _recvBits |= 1u << (diff - 1);
                _recvHigh = id;
            }
            else if (diff == 0) fresh = false;
            else
            {
                int idx = -diff - 1;
                if (idx >= 32 || (_recvBits & (1u << idx)) != 0) fresh = false;
                else _recvBits |= 1u << idx;
            }
        }
        if (!fresh) return true;

        p = HeaderBytes;
        bool anyItem = false;
        while (p < packet.Length)
        {
            byte flags = packet[p++];
            anyItem = true;
            if ((flags & 1) != 0)
            {
                ushort seq = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(p, 2)); p += 2;
                int len = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(p, 2)); p += 2;
                OnUnit(seq, (flags & 2) != 0, packet.Slice(p, len));
                p += len;
            }
            else
            {
                int len = BinaryPrimitives.ReadUInt16LittleEndian(packet.Slice(p, 2)); p += 2;
                _messages.Enqueue(packet.Slice(p, len).ToArray());
                p += len;
            }
        }
        if (anyItem) _ackDirty = true;
        return true;
    }

    /// <summary>Takes the next complete message (reliable ones in send order, unreliable ones as they arrive).</summary>
    public bool TryDequeueMessage(out byte[] message)
    {
        if (_messages.Count > 0) { message = _messages.Dequeue(); return true; }
        message = Array.Empty<byte>();
        return false;
    }

    private void OnUnit(ushort seq, bool more, ReadOnlySpan<byte> data)
    {
        int d = (ushort)(seq - _recvNext);
        if (d >= ReceiveWindow) return;   // old duplicate or absurdly far ahead
        if (d != 0)
        {
            _recvBuf.TryAdd(seq, (more, data.ToArray()));
            return;
        }
        Deliver(more, data);
        _recvNext++;
        while (_recvBuf.Remove(_recvNext, out var next))
        {
            Deliver(next.More, next.Data);
            _recvNext++;
        }
    }

    private void Deliver(bool more, ReadOnlySpan<byte> data)
    {
        if (!more && _assembling.Count == 0)
        {
            _messages.Enqueue(data.ToArray());
            return;
        }
        if (_assembling.Count + data.Length > MaxMessageBytes) { Faulted = true; _assembling.Clear(); return; }
        foreach (byte b in data) _assembling.Add(b);
        if (!more)
        {
            _messages.Enqueue(_assembling.ToArray());
            _assembling.Clear();
        }
    }

    private void ProcessAcks(long now, ushort ack, uint bits)
    {
        AckPacket(now, ack);
        for (int i = 0; i < 32; i++)
            if ((bits & (1u << i)) != 0) AckPacket(now, (ushort)(ack - 1 - i));
        int trim = 0;
        while (trim < _units.Count && _units[trim].Acked) trim++;
        if (trim > 0) _units.RemoveRange(0, trim);
    }

    private void AckPacket(long now, ushort id)
    {
        SentPacket sp = _sent[id % SentRing];
        if (sp.Id != id) return;
        sp.Id = -1;
        long sample = now - sp.SentAt;
        if (sample >= 0)
            _srtt = _srtt < 0 ? sample : (_srtt * 7 + sample) / 8;
        foreach (Unit u in sp.Units) u.Acked = true;
        sp.Units.Clear();
    }
}
