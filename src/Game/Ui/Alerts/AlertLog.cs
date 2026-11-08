// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Game.Ui.Alerts;

/// <summary>One line of the stack as it is drawn: text, tone, merge count, and opacity.</summary>
public readonly record struct AlertLine(AlertKind Kind, string Text, int Count, byte Alpha);

/// <summary>One minimap ping: where, how old (0..1 of its life) and whether it is red.</summary>
public readonly record struct AlertPing(LeptonCoord Pos, float Age, bool Danger);

/// <summary>One place the last-alert key can jump to.</summary>
public readonly record struct JumpTarget(AlertKind Kind, LeptonCoord Pos, int Tick);

/// <summary>
/// The pure state behind the alert stack, the minimap pings and the last-alert key. Merges repeats into one line, rate-limits each
/// alert per area, keeps the newest at the top (at most <see cref="MaxLines"/>), fades lines and pings, and remembers the last few
/// places. All timing is sim ticks; nothing here reads a clock.
/// </summary>
public sealed class AlertLog
{
    /// <summary>Most lines shown at once.</summary>
    public const int MaxLines = 4;
    /// <summary>Ticks a line stays (about 4.5 s).</summary>
    public const int LifeTicks = 90;
    /// <summary>The last ticks of a line's life, over which it fades out.</summary>
    public const int FadeTicks = 20;
    /// <summary>Ticks a minimap ping lasts (4 s).</summary>
    public const int PingTicks = 80;
    /// <summary>Most places remembered for the last-alert key.</summary>
    public const int JumpMax = 6;
    /// <summary>Most pings alive at once.</summary>
    public const int PingMax = 12;
    /// <summary>Side of an "area" in cells: attacks inside the same square count as one place.</summary>
    public const int AreaCells = 12;

    private readonly record struct Key(AlertKind Kind, int AreaX, int AreaY);

    private sealed class Line
    {
        public Key Key;
        public AlertKind Kind;
        public int LastTick;
        public int CountTick;
        public int Count = 1;
    }

    private sealed class Ping
    {
        public LeptonCoord Pos;
        public int Start;
        public bool Danger;
    }

    private sealed class Jump
    {
        public Key Key;
        public AlertKind Kind;
        public LeptonCoord Pos;
        public int Tick;
    }

    private readonly List<Line> _lines = new();              // oldest first
    private readonly List<Ping> _pings = new();
    private readonly List<Jump> _jumps = new();              // newest first
    private readonly Dictionary<Key, int> _lastCounted = new();

    /// <summary>Changes whenever a new place enters the jump list (not when an existing one is refreshed), so the jump cursor knows to start over.</summary>
    public int JumpVersion { get; private set; }

    /// <summary>Forgets everything (a new game).</summary>
    public void Clear()
    {
        _lines.Clear();
        _pings.Clear();
        _jumps.Clear();
        _lastCounted.Clear();
        JumpVersion++;
    }

    /// <summary>
    /// Reports one alert. A repeat of a line still showing refreshes it; a new one inside the rate-limit window is dropped; otherwise
    /// a line (and a ping, when it has a place) is added. Returns true when the alert was counted (new line or counted repeat).
    /// </summary>
    public bool Add(GameAlert a)
    {
        int now = a.Tick;
        Key key = KeyOf(a);
        int interval = AlertInfo.IntervalTicks(a.Kind);
        Prune(now);
        Line? line = _lines.Find(l => l.Key == key);
        bool counted;
        if (line is not null)
        {
            line.LastTick = now;
            counted = now - line.CountTick >= interval;
            if (counted) { line.Count++; line.CountTick = now; _lastCounted[key] = now; }
            if (a.Pos is { } p) RefreshJump(key, a, p, isNew: false);
        }
        else
        {
            if (_lastCounted.TryGetValue(key, out int last) && now - last < interval && now >= last) return false;
            _lines.Add(new Line { Key = key, Kind = a.Kind, LastTick = now, CountTick = now });
            _lastCounted[key] = now;
            counted = true;
            if (a.Pos is { } p) RefreshJump(key, a, p, isNew: true);
        }

        if (counted && a.Pos is { } at)
        {
            _pings.Add(new Ping { Pos = at, Start = now, Danger = AlertInfo.IsDanger(a.Kind) });
            if (_pings.Count > PingMax) _pings.RemoveAt(0);
        }

        return counted;
    }

    /// <summary>The lines to draw at <paramref name="tick"/>, newest first, at most <see cref="MaxLines"/>.</summary>
    public IReadOnlyList<AlertLine> Lines(int tick)
    {
        Prune(tick);
        var result = new List<AlertLine>(MaxLines);
        for (int i = _lines.Count - 1; i >= 0 && result.Count < MaxLines; i--)
        {
            Line l = _lines[i];
            int left = LifeTicks - (tick - l.LastTick);
            byte alpha = left >= FadeTicks ? (byte)255 : (byte)Math.Clamp(255 * left / FadeTicks, 0, 255);
            string text = AlertInfo.Text(l.Kind);
            if (l.Count > 1 && AlertInfo.ShowsCount(l.Kind)) text += " x" + l.Count;
            result.Add(new AlertLine(l.Kind, text, l.Count, alpha));
        }

        return result;
    }

    /// <summary>The minimap pings alive at <paramref name="tick"/>.</summary>
    public IReadOnlyList<AlertPing> Pings(int tick)
    {
        Prune(tick);
        var result = new List<AlertPing>(_pings.Count);
        foreach (Ping p in _pings) result.Add(new AlertPing(p.Pos, Math.Clamp((tick - p.Start) / (float)PingTicks, 0f, 1f), p.Danger));
        return result;
    }

    /// <summary>The remembered places, most recent first (at most <see cref="JumpMax"/>).</summary>
    public IReadOnlyList<JumpTarget> JumpTargets()
    {
        var result = new List<JumpTarget>(_jumps.Count);
        foreach (Jump j in _jumps) result.Add(new JumpTarget(j.Kind, j.Pos, j.Tick));
        return result;
    }

    private static Key KeyOf(GameAlert a)
    {
        if (!AlertInfo.PerArea(a.Kind) || a.Pos is not { } p) return new Key(a.Kind, 0, 0);
        int unit = AreaCells * SimConstants.LeptonsPerCell;
        return new Key(a.Kind, FloorDiv(p.X, unit), FloorDiv(p.Y, unit));
    }

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    private void RefreshJump(Key key, GameAlert a, LeptonCoord pos, bool isNew)
    {
        if (!AlertInfo.InJumpList(a.Kind)) return;
        int i = _jumps.FindIndex(j => j.Key == key);
        if (i >= 0)
        {
            Jump j = _jumps[i];
            j.Pos = pos;
            j.Tick = a.Tick;
            if (isNew && i != 0) { _jumps.RemoveAt(i); _jumps.Insert(0, j); JumpVersion++; }
            else if (isNew) JumpVersion++;
            return;
        }

        _jumps.Insert(0, new Jump { Key = key, Kind = a.Kind, Pos = pos, Tick = a.Tick });
        if (_jumps.Count > JumpMax) _jumps.RemoveAt(_jumps.Count - 1);
        JumpVersion++;
    }

    private void Prune(int now)
    {
        _lines.RemoveAll(l => now - l.LastTick >= LifeTicks || now < l.LastTick);
        _pings.RemoveAll(p => now - p.Start >= PingTicks || now < p.Start);
    }
}
