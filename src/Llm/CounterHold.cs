// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Sim.Core;

namespace Rts.Llm;

/// <summary>
/// S47 (M16-A-08, design 15 idea 2): "counter this for N minutes". A directive with a counter mix (anti-air, anti-armour, anti-infantry) and
/// <see cref="DirectiveDraft.CounterMinutes"/> above 0 is sent with the strong flag, and the same mix is kept in every later directive whose
/// mix is auto until the window ends; the model naming another mix ends the hold at once. When the window ends with no new directive,
/// <see cref="Expire"/> gives the last directive back with mix auto and no strong flag. All of it happens before the directive becomes a
/// command, so the sim and the replay only ever see ordinary directives (D-110): nothing here is saved or hashed. Game thread only.
/// </summary>
public sealed class CounterHold
{
    /// <summary>Game ticks per game minute (20 ticks per second).</summary>
    public const int TicksPerMinute = 60 * DirectiveCommands.TicksPerSecond;

    private byte _mix;
    private int _until = -1;

    /// <summary>True for the three counter mixes (anti-air, anti-armour, anti-infantry).</summary>
    public static bool IsCounter(byte mix) =>
        mix == DirectiveCodec.MixAntiAir || mix == DirectiveCodec.MixAntiArmour || mix == DirectiveCodec.MixAntiInfantry;

    /// <summary>True while a hold is running at <paramref name="tick"/>.</summary>
    public bool Active(int tick) => _until >= 0 && tick < _until;

    /// <summary>The mix being held, or auto when none.</summary>
    public byte HeldMix => _until >= 0 ? _mix : DirectiveCodec.MixAuto;

    /// <summary>Tick the hold ends, or -1 when there is none.</summary>
    public int Until => _until;

    /// <summary>The draft to send at <paramref name="tick"/>: starts, keeps or ends the hold as described on the type.</summary>
    public DirectiveDraft Apply(DirectiveDraft d, int tick)
    {
        if (d.CounterMinutes > 0 && IsCounter(d.Mix))
        {
            _mix = d.Mix;
            _until = tick + d.CounterMinutes * TicksPerMinute;
            return d with { StrongMix = true };
        }
        if (_until >= 0 && tick >= _until) _until = -1;   // ran out without Expire being asked: a reply arrived first
        if (Active(tick))
        {
            if (d.Mix == DirectiveCodec.MixAuto) return d with { Mix = _mix, StrongMix = true, CounterMinutes = 0 };
            _until = -1;   // the model chose a mix of its own: the hold is over
        }
        return d;
    }

    /// <summary>When a hold has run out at <paramref name="tick"/> (and nothing else ended it), returns <paramref name="last"/> with mix auto and
    /// no strong flag so production goes back to the plain AI; null otherwise. Clears the hold.</summary>
    public DirectiveDraft? Expire(DirectiveDraft? last, int tick)
    {
        if (_until < 0 || tick < _until) return null;
        _until = -1;
        if (last is not { } d || d.Mix != _mix) return null;
        return d with { Mix = DirectiveCodec.MixAuto, StrongMix = false, CounterMinutes = 0 };
    }
}
