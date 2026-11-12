// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using System.Buffers.Binary;
using System.Text;
using Rts.Sim.Core;

namespace Rts.Net;

/// <summary>
/// Binary encoding of <see cref="NetMessage"/> (M9 contract). Little-endian, one type byte first, strings as UTF-8 with a length
/// prefix, arrays with a count prefix. Decode rejects anything malformed with <see cref="InvalidDataException"/> (never another
/// exception type), so a bad packet can be dropped without touching the game. Strings are capped at <see cref="MaxStringBytes"/>
/// bytes (chat text at <see cref="MaxChatChars"/> characters); Encode shortens a longer string, Decode refuses one. Arrays and blobs
/// have hard caps that are checked against the bytes actually present before anything is allocated.
/// </summary>
public static class NetCodec
{
    /// <summary>Longest string on the wire, in UTF-8 bytes.</summary>
    public const int MaxStringBytes = 512;

    /// <summary>Longest chat text, in characters.</summary>
    public const int MaxChatChars = 200;

    /// <summary>Largest opaque blob (lobby state, slot request, start settings), in bytes.</summary>
    public const int MaxBlobBytes = 256 * 1024;

    /// <summary>Most commands in one Input or TurnBundle.</summary>
    public const int MaxCommands = 4096;

    /// <summary>Most sub-hashes in one Hash message.</summary>
    public const int MaxSubHashes = 64;

    /// <summary>Most peer ids in one Waiting or Desync message.</summary>
    public const int MaxPeerList = 64;

    /// <summary>Largest FileChunk payload (a chunk fits one packet).</summary>
    public const int MaxChunkBytes = 1000;

    /// <summary>Bytes one encoded command takes.</summary>
    public const int CommandBytes = 32;

    private static readonly UTF8Encoding Utf8 = new(false, true);

    private enum Tag : byte
    {
        Hello = 1, Welcome, Reject, LobbyState, SlotRequest, Ready, Start, Input, TurnBundle, Progress, Hash, Chat,
        Pause, Resume, Waiting, PeerDropped, Desync, ResyncBegin, FileChunk, Ping, Pong, Bye,
    }

    /// <summary>Encodes a message. Throws <see cref="ArgumentException"/> for an array or blob over its cap (a caller bug).</summary>
    public static byte[] Encode(NetMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var w = new Writer();
        switch (message)
        {
            case NetMessage.Hello m:
                w.Tag(Tag.Hello); w.I32(m.Protocol); w.Str(m.BuildVersion); w.U64(m.ContentHash); w.Str(m.PlayerName); w.Bool(m.Spectator); break;
            case NetMessage.Welcome m: w.Tag(Tag.Welcome); w.I32(m.PeerId); break;
            case NetMessage.Reject m: w.Tag(Tag.Reject); w.Str(m.Reason); break;
            case NetMessage.LobbyState m: w.Tag(Tag.LobbyState); w.Blob(m.Blob, MaxBlobBytes); break;
            case NetMessage.SlotRequest m: w.Tag(Tag.SlotRequest); w.Blob(m.Blob, MaxBlobBytes); break;
            case NetMessage.Ready m: w.Tag(Tag.Ready); w.Bool(m.IsReady); break;
            case NetMessage.Start m: w.Tag(Tag.Start); w.I32(m.CommandDelay); w.Blob(m.SetupBlob, MaxBlobBytes); break;
            case NetMessage.Input m: w.Tag(Tag.Input); w.Commands(m.Commands); break;
            case NetMessage.TurnBundle m: w.Tag(Tag.TurnBundle); w.I32(m.Tick); w.Commands(m.Commands); break;
            case NetMessage.Progress m: w.Tag(Tag.Progress); w.I32(m.SteppedTick); break;
            case NetMessage.Hash m:
                w.Tag(Tag.Hash); w.I32(m.Tick); w.U64(m.Value);
                if (m.SubHashes.Length > MaxSubHashes) throw new ArgumentException("Too many sub-hashes.");
                w.U16((ushort)m.SubHashes.Length);
                foreach (ulong h in m.SubHashes) w.U64(h);
                break;
            case NetMessage.Chat m: w.Tag(Tag.Chat); w.I32(m.FromPeer); w.U8(m.Audience); w.Str(ClipChat(m.Text)); break;
            case NetMessage.Pause m: w.Tag(Tag.Pause); w.I32(m.ByPeer); break;
            case NetMessage.Resume m: w.Tag(Tag.Resume); w.I32(m.ByPeer); w.I32(m.CountdownMs); break;
            case NetMessage.Waiting m: w.Tag(Tag.Waiting); w.IntList(m.Peers); w.I32(m.WaitedMs); break;
            case NetMessage.PeerDropped m: w.Tag(Tag.PeerDropped); w.I32(m.Peer); w.I32(m.Tick); w.Str(m.Reason); break;
            case NetMessage.Desync m: w.Tag(Tag.Desync); w.I32(m.Tick); w.IntList(m.Peers); break;
            case NetMessage.ResyncBegin m: w.Tag(Tag.ResyncBegin); w.I32(m.Tick); w.I32(m.TransferId); break;
            case NetMessage.FileChunk m:
                w.Tag(Tag.FileChunk); w.U8(m.Kind); w.I32(m.TransferId); w.I32(m.Index); w.I32(m.Count); w.Blob(m.Data, MaxChunkBytes); break;
            case NetMessage.Ping m: w.Tag(Tag.Ping); w.I64(m.Stamp); break;
            case NetMessage.Pong m: w.Tag(Tag.Pong); w.I64(m.Stamp); break;
            case NetMessage.Bye m: w.Tag(Tag.Bye); w.Str(m.Reason); break;
            default: throw new ArgumentException("Unknown message type " + message.GetType().Name);
        }
        return w.ToArray();
    }

    /// <summary>Decodes one message; throws <see cref="InvalidDataException"/> on malformed input.</summary>
    public static NetMessage Decode(ReadOnlySpan<byte> data)
    {
        try
        {
            var r = new Reader(data);
            NetMessage m = (Tag)r.U8() switch
            {
                Tag.Hello => new NetMessage.Hello(r.I32(), r.Str(), r.U64(), r.Str(), r.Bool()),
                Tag.Welcome => new NetMessage.Welcome(r.I32()),
                Tag.Reject => new NetMessage.Reject(r.Str()),
                Tag.LobbyState => new NetMessage.LobbyState(r.Blob(MaxBlobBytes)),
                Tag.SlotRequest => new NetMessage.SlotRequest(r.Blob(MaxBlobBytes)),
                Tag.Ready => new NetMessage.Ready(r.Bool()),
                Tag.Start => new NetMessage.Start(r.I32(), r.Blob(MaxBlobBytes)),
                Tag.Input => new NetMessage.Input(r.Commands()),
                Tag.TurnBundle => DecodeBundle(ref r),
                Tag.Progress => new NetMessage.Progress(r.I32()),
                Tag.Hash => DecodeHash(ref r),
                Tag.Chat => DecodeChat(ref r),
                Tag.Pause => new NetMessage.Pause(r.I32()),
                Tag.Resume => new NetMessage.Resume(r.I32(), r.I32()),
                Tag.Waiting => DecodeWaiting(ref r),
                Tag.PeerDropped => new NetMessage.PeerDropped(r.I32(), r.I32(), r.Str()),
                Tag.Desync => DecodeDesync(ref r),
                Tag.ResyncBegin => new NetMessage.ResyncBegin(r.I32(), r.I32()),
                Tag.FileChunk => DecodeChunk(ref r),
                Tag.Ping => new NetMessage.Ping(r.I64()),
                Tag.Pong => new NetMessage.Pong(r.I64()),
                Tag.Bye => new NetMessage.Bye(r.Str()),
                _ => throw new InvalidDataException("Unknown message type."),
            };
            if (r.Remaining != 0) throw new InvalidDataException("Trailing bytes after message.");
            return m;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException("Malformed message.", e);
        }
    }

    private static NetMessage DecodeBundle(ref Reader r)
    {
        int tick = r.I32();
        return new NetMessage.TurnBundle(tick, r.Commands());
    }

    private static NetMessage DecodeHash(ref Reader r)
    {
        int tick = r.I32();
        ulong value = r.U64();
        int n = r.U16();
        if (n > MaxSubHashes) throw new InvalidDataException("Too many sub-hashes.");
        r.Need(n * 8);
        var subs = new ulong[n];
        for (int i = 0; i < n; i++) subs[i] = r.U64();
        return new NetMessage.Hash(tick, value, subs);
    }

    private static NetMessage DecodeChat(ref Reader r)
    {
        int from = r.I32();
        byte audience = r.U8();
        string text = r.Str();
        if (text.Length > MaxChatChars) throw new InvalidDataException("Chat text too long.");
        return new NetMessage.Chat(from, audience, text);
    }

    private static NetMessage DecodeWaiting(ref Reader r)
    {
        int[] peers = r.IntList();
        return new NetMessage.Waiting(peers, r.I32());
    }

    private static NetMessage DecodeDesync(ref Reader r)
    {
        int tick = r.I32();
        return new NetMessage.Desync(tick, r.IntList());
    }

    private static NetMessage DecodeChunk(ref Reader r)
    {
        byte kind = r.U8();
        int id = r.I32(), index = r.I32(), count = r.I32();
        if (count < 1 || index < 0 || index >= count) throw new InvalidDataException("Bad chunk index.");
        return new NetMessage.FileChunk(kind, id, index, count, r.Blob(MaxChunkBytes));
    }

    private static string ClipChat(string text)
    {
        if (text.Length <= MaxChatChars) return text;
        int n = MaxChatChars;
        if (char.IsHighSurrogate(text[n - 1])) n--;
        return text[..n];
    }

    private sealed class Writer
    {
        private byte[] _buf = new byte[64];
        private int _len;

        public byte[] ToArray() => _buf.AsSpan(0, _len).ToArray();

        private Span<byte> Take(int n)
        {
            if (_len + n > _buf.Length) Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + n));
            Span<byte> s = _buf.AsSpan(_len, n);
            _len += n;
            return s;
        }

        public void Tag(Tag t) => U8((byte)t);
        public void U8(byte v) => Take(1)[0] = v;
        public void Bool(bool v) => U8(v ? (byte)1 : (byte)0);
        public void U16(ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(Take(2), v);
        public void I32(int v) => BinaryPrimitives.WriteInt32LittleEndian(Take(4), v);
        public void I64(long v) => BinaryPrimitives.WriteInt64LittleEndian(Take(8), v);
        public void U64(ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(Take(8), v);

        public void Str(string s)
        {
            ArgumentNullException.ThrowIfNull(s);
            if (Utf8.GetByteCount(s) > MaxStringBytes)
            {
                // Cut at a code point boundary so the result is still valid UTF-8.
                int bytes = 0, chars = 0;
                foreach (Rune rune in s.EnumerateRunes())
                {
                    int b = rune.Utf8SequenceLength;
                    if (bytes + b > MaxStringBytes) break;
                    bytes += b; chars += rune.Utf16SequenceLength;
                }
                s = s[..chars];
            }
            byte[] b8 = Utf8.GetBytes(s);
            U16((ushort)b8.Length);
            b8.CopyTo(Take(b8.Length));
        }

        public void Blob(byte[] b, int max)
        {
            ArgumentNullException.ThrowIfNull(b);
            if (b.Length > max) throw new ArgumentException("Blob too large.");
            I32(b.Length);
            b.CopyTo(Take(b.Length));
        }

        public void IntList(int[] a)
        {
            ArgumentNullException.ThrowIfNull(a);
            if (a.Length > MaxPeerList) throw new ArgumentException("Too many peers.");
            U16((ushort)a.Length);
            foreach (int v in a) I32(v);
        }

        public void Commands(Command[] cs)
        {
            ArgumentNullException.ThrowIfNull(cs);
            if (cs.Length > MaxCommands) throw new ArgumentException("Too many commands.");
            U16((ushort)cs.Length);
            foreach (Command c in cs)
            {
                I32(c.ExecTick); U8(c.Player); U16(c.Sequence); U8((byte)c.Kind);
                U16(c.Subject.Index); U16(c.Subject.Generation); U16(c.Target.Index); U16(c.Target.Generation);
                I32(c.Cell.X); I32(c.Cell.Y); I32(c.Arg); I32(c.Arg2);
            }
        }
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _d;
        private int _p;

        public Reader(ReadOnlySpan<byte> d) { _d = d; _p = 0; }

        public readonly int Remaining => _d.Length - _p;

        public readonly void Need(int n)
        {
            if (n < 0 || n > _d.Length - _p) throw new InvalidDataException("Message is truncated.");
        }

        private ReadOnlySpan<byte> Take(int n)
        {
            Need(n);
            ReadOnlySpan<byte> s = _d.Slice(_p, n);
            _p += n;
            return s;
        }

        public byte U8() => Take(1)[0];

        public bool Bool()
        {
            byte b = U8();
            if (b > 1) throw new InvalidDataException("Bad boolean.");
            return b == 1;
        }

        public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        public long I64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
        public ulong U64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));

        public string Str()
        {
            int n = U16();
            if (n > MaxStringBytes) throw new InvalidDataException("String too long.");
            try { return Utf8.GetString(Take(n)); }
            catch (DecoderFallbackException e) { throw new InvalidDataException("Bad UTF-8.", e); }
        }

        public byte[] Blob(int max)
        {
            int n = I32();
            if (n < 0 || n > max) throw new InvalidDataException("Blob size out of range.");
            return Take(n).ToArray();
        }

        public int[] IntList()
        {
            int n = U16();
            if (n > MaxPeerList) throw new InvalidDataException("Too many peers.");
            Need(n * 4);
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = I32();
            return a;
        }

        public Command[] Commands()
        {
            int n = U16();
            if (n > MaxCommands) throw new InvalidDataException("Too many commands.");
            Need(n * CommandBytes);
            var a = new Command[n];
            for (int i = 0; i < n; i++)
            {
                int exec = I32(); byte player = U8(); ushort seq = U16(); var kind = (CommandKind)U8();
                var subject = new EntityId(U16(), U16());
                var target = new EntityId(U16(), U16());
                var cell = new CellCoord(I32(), I32());
                int arg = I32(), arg2 = I32();
                a[i] = new Command(exec, player, seq, kind, subject, target, cell, arg, arg2);
            }
            return a;
        }
    }
}
