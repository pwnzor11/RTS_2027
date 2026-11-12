// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Net.Tests.Udp;

/// <summary>A deterministic lossy datagram wire between two endpoints: fixed-seed loss, duplication, delay and reordering.</summary>
internal sealed class FakeWire
{
    private readonly Random _rng;
    private readonly double _loss, _dup;
    private readonly long _baseDelay, _jitter;
    private readonly List<(long At, long Order, int To, byte[] Data)> _inFlight = new();
    private long _order;

    public int Sent, Dropped, Duplicated;

    public FakeWire(int seed, double loss, double duplicate, long baseDelayMicros, long jitterMicros)
    {
        _rng = new Random(seed);
        _loss = loss; _dup = duplicate; _baseDelay = baseDelayMicros; _jitter = jitterMicros;
    }

    public void Send(long now, int to, byte[] data, int length)
    {
        Sent++;
        if (_rng.NextDouble() < _loss) { Dropped++; return; }
        int copies = _rng.NextDouble() < _dup ? 2 : 1;
        if (copies == 2) Duplicated++;
        for (int i = 0; i < copies; i++)
            _inFlight.Add((now + _baseDelay + (long)(_rng.NextDouble() * _jitter), _order++, to, data.AsSpan(0, length).ToArray()));
    }

    public IEnumerable<(int To, byte[] Data)> Deliver(long now)
    {
        var due = _inFlight.Where(p => p.At <= now).OrderBy(p => p.At).ThenBy(p => p.Order).ToList();
        _inFlight.RemoveAll(p => p.At <= now);
        foreach (var p in due) yield return (p.To, p.Data);
    }

    public int InFlight => _inFlight.Count;
}
