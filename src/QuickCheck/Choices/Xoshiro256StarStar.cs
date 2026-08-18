namespace QuickCheck.Choices;

/// <summary>
/// A xoshiro256** pseudo-random number generator seeded through SplitMix64.
/// </summary>
/// <remarks>
/// The library owns its PRNG rather than using <see cref="Random"/> so that a
/// seed reproduces the same choice sequence on every runtime version and
/// platform, which is what makes <see cref="CheckOptions.Seed"/> replayable.
/// </remarks>
internal sealed class Xoshiro256StarStar
{
    private ulong _s0, _s1, _s2, _s3;

    private Xoshiro256StarStar(ulong seed)
    {
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
    }

    /// <summary>
    /// Creates a generator for one test case. Every (<paramref name="seed"/>,
    /// <paramref name="run"/>) pair yields an independent, reproducible stream,
    /// so a single failing run can be replayed without regenerating those
    /// before it.
    /// </summary>
    public static Xoshiro256StarStar ForRun(ulong seed, int run)
    {
        var mixed = seed;
        var a = SplitMix64(ref mixed);
        var b = SplitMix64(ref mixed) ^ (ulong)run;
        return new Xoshiro256StarStar(a ^ (b * 0x9E3779B97F4A7C15UL));
    }

    public ulong NextUInt64()
    {
        var result = RotateLeft(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);

        return result;
    }

    /// <summary>Returns a uniformly distributed value in [0, max].</summary>
    public ulong NextUInt64Inclusive(ulong max)
    {
        if (max == ulong.MaxValue)
        {
            return NextUInt64();
        }

        // Rejection sampling over the smallest power-of-two mask covering max.
        var mask = ulong.MaxValue >> System.Numerics.BitOperations.LeadingZeroCount(max | 1);

        while (true)
        {
            var candidate = NextUInt64() & mask;

            if (candidate <= max)
            {
                return candidate;
            }
        }
    }

    /// <summary>Returns a value in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    private static ulong SplitMix64(ref ulong state)
    {
        var z = state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
