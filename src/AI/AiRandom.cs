// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) RTS_2027 contributors. See LICENSE (incl. the MonoGame linking permission).
namespace Rts.AI;

/// <summary>Small deterministic generator for AI decisions (splitmix64), seeded from (game seed, slot). Not sim state.</summary>
public sealed class AiRandom
{
    private ulong _state;

    /// <summary>Seeds from the game seed and the player slot.</summary>
    public AiRandom(ulong gameSeed, int slot)
    {
        _state = gameSeed ^ (0x9E3779B97F4A7C15UL * (ulong)(slot + 1));
        NextULong();
    }

    /// <summary>Next 64 random bits.</summary>
    public ulong NextULong()
    {
        unchecked
        {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>Integer in [0, maxExclusive).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return (int)(NextULong() % (ulong)maxExclusive);
    }
}
