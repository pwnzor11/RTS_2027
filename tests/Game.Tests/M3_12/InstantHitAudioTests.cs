// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
using Rts.Game.Audio;
using Rts.Sim.Core;
using static Rts.Game.Tests.Audio.AudioTestSupport;

namespace Rts.Game.Tests.M3_12;

/// <summary>M3-12: an instant hit shows an impact but makes no shell or explosion sound (small arms keep their bullet sound).</summary>
public sealed class InstantHitAudioTests
{
    [Fact]
    public void An_instant_hit_makes_no_explosion_or_shell_sound()
    {
        SimSnapshot a = Snap(1);
        SimSnapshot b = Snap(2, fx: new[] { new FxEvent(FxKind.InstantHit, LeptonCoord.CenterOf(new CellCoord(4, 4)), 1) });
        List<AudioEvent> events = Derive(new AudioEventDeriver(), Frame(a, b, Hud(), Hud()));
        Assert.DoesNotContain(events, e => e.Kind is AudioEventKind.ExplosionSmall or AudioEventKind.ExplosionLarge or AudioEventKind.FireShell);
    }
}
