// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.Game.Menu;

/// <summary>A tiny deterministic generator (SplitMix32 style) for the menu's procedural art. Not used by the simulation.</summary>
public struct SceneRng
{
    private uint _state;

    /// <summary>Starts a stream from a seed.</summary>
    public SceneRng(int seed) => _state = (uint)seed * 2654435761u + 0x9E3779B9u;

    /// <summary>Next value in 0..1 (exclusive of 1).</summary>
    public float Next()
    {
        _state += 0x9E3779B9u;
        uint z = _state;
        z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
        z = (z ^ (z >> 13)) * 0xC2B2AE35u;
        z ^= z >> 16;
        return (z >> 8) / 16777216f;
    }

    /// <summary>Next value in <paramref name="min"/>..<paramref name="max"/>.</summary>
    public float Range(float min, float max) => min + (max - min) * Next();

    /// <summary>A stable 0..1 value for a pair of integers (no stream state).</summary>
    public static float Hash(int a, int b)
    {
        var r = new SceneRng(a * 7919 + b * 104729 + 17);
        return r.Next();
    }
}
