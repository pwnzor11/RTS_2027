// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).

namespace Rts.Game.Render;

/// <summary>
/// The only source of "randomness" for decals and props: a 32-bit integer mix of (map hash, x, y, salt). Pure and platform independent,
/// so the same map gives the same decor on every machine and in every screenshot. Render only; the simulation never reads it.
/// </summary>
public static class DecorHash
{
    /// <summary>Mixes the four inputs into 32 well-spread bits.</summary>
    public static uint Mix(ulong seed, int x, int y, uint salt)
    {
        unchecked
        {
            uint h = (uint)seed ^ (uint)(seed >> 32) * 0x9E3779B1u;
            h += (uint)x * 0x85EBCA77u + (uint)y * 0xC2B2AE3Du + salt * 0x27D4EB2Fu + 0x165667B1u;
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;
            return h;
        }
    }
}
