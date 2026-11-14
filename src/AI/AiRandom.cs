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
        // M13-27 (s25): each half goes through the splitmix finaliser first. The old seed (gameSeed ^ gamma * (slot + 1)) left slot 1's state
        // exactly one gamma step ahead of slot 0's for about a quarter of even game seeds, so the two AIs drew the same sequence shifted by one
        // draw, which skewed identical mirrors by game-seed parity.
        _state = Mix(gameSeed) ^ Mix(0xD1B54A32D192ED03UL * (ulong)(slot + 1));
        NextULong();
    }

    private static ulong Mix(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <summary>M3-21: the generator's whole state, so a save can write it and a load can continue the same sequence.</summary>
    public ulong State { get => _state; set => _state = value; }

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
