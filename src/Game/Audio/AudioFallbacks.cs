// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Audio;

/// <summary>
/// The event that stands in for another when sounds.toml has no entry for it (S22), so the game plays exactly what it played before the
/// new keys existed whenever the new sound files are missing: a unit death is a small explosion, a destroyed building a large one, a
/// water splash an explosion, a ship horn a unit-ready cue. Events with no stand-in (sonar, flyby, hover, rig sounds) are silent.
/// </summary>
public static class AudioFallbacks
{
    /// <summary>The stand-in for <paramref name="e"/>, or null when there is none.</summary>
    public static AudioEvent? Next(AudioEvent e) => e.Kind switch
    {
        AudioEventKind.UnitDied => new AudioEvent(AudioEventKind.ExplosionSmall, e.Pos),
        AudioEventKind.BuildingDestroyed => new AudioEvent(AudioEventKind.ExplosionLarge, e.Pos),
        AudioEventKind.WaterImpact => new AudioEvent(e.Class == "large" ? AudioEventKind.ExplosionLarge : AudioEventKind.ExplosionSmall, e.Pos),
        AudioEventKind.ShipHorn => new AudioEvent(AudioEventKind.UnitReady, e.Pos, "ship", e.Faction),
        _ => null,
    };
}
