// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Sim.Core;

/// <summary>What an <see cref="FxEvent"/> reports.</summary>
public enum FxKind : byte
{
    /// <summary>A shell or splash weapon went off (a projectile or an instant splash weapon's blast).</summary>
    Detonation = 0,
    /// <summary>A building was removed this tick with no health left.</summary>
    BuildingDestroyed = 1,
    /// <summary>A vehicle or infantry actor was removed this tick with no health left.</summary>
    UnitDestroyed = 2,
    /// <summary>M3-13: a superweapon went off. Pos = the target point, Size = the blast radius in cells (flash and screen shake).</summary>
    Superweapon = 3,
    /// <summary>An instant (hitscan) single-target weapon hit its point this tick (M3-12). Kept apart from
    /// <see cref="Detonation"/> so the audio, which counts shells by detonations, does not mistake a rifle hit for one.</summary>
    InstantHit = 4,
}

/// <summary>
/// A one-tick effects hint for the renderer (M2-19): something exploded or died during the last Step. Not sim state: it
/// is never hashed or saved, and lives only until the next Step. Pos = impact point or the actor's position (a building's
/// centre). Size = splash radius in cells for a detonation (at least 1), the larger footprint side for a building, 1 for a unit.
/// </summary>
public readonly record struct FxEvent(FxKind Kind, LeptonCoord Pos, int Size);
