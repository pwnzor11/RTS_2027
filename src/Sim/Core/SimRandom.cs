namespace Rts.Sim.Core;

/// <summary>
/// PCG32 (XSH RR, 64-bit state). The only RNG sim code may use. State is saved and hashed.
/// Reference: O'Neill, pcg-random.org. Our own implementation; no System.Random.
/// </summary>
public sealed class SimRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    private ulong _state;
    private ulong _inc;

    public SimRandom(ulong seed, ulong stream = 54UL)
    {
        _state = 0;
        _inc = (stream << 1) | 1UL;
        NextUInt();
        _state += seed;
        NextUInt();
    }

    private SimRandom(ulong state, ulong inc, bool _) { _state = state; _inc = inc; }

    public uint NextUInt()
    {
        ulong old = _state;
        _state = unchecked(old * Multiplier + _inc);
        uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
    }

    /// <summary>Unbiased integer in [0, maxExclusive). maxExclusive must be &gt; 0.</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        uint bound = (uint)maxExclusive;
        uint threshold = unchecked((uint)(-(int)bound)) % bound;
        while (true)
        {
            uint r = NextUInt();
            if (r >= threshold) return (int)(r % bound);
        }
    }

    /// <summary>Integer in [minInclusive, maxExclusive).</summary>
    public int Next(int minInclusive, int maxExclusive) => minInclusive + Next(maxExclusive - minInclusive);

    public (ulong State, ulong Inc) GetState() => (_state, _inc);
    public static SimRandom FromState(ulong state, ulong inc) => new(state, inc, true);
}
