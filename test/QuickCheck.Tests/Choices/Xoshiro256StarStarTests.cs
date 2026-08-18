using QuickCheck.Choices;

namespace QuickCheck.Tests.Choices;

public sealed class Xoshiro256StarStarTests
{
    /// <summary>
    /// Known-answer vectors produced by the reference C implementation
    /// (https://prng.di.unimi.it/xoshiro256starstar.c) seeded through the
    /// reference splitmix64 with the same derivation as
    /// <see cref="Xoshiro256StarStar.ForRun"/>. Any change to these values
    /// breaks replay of previously recorded seeds.
    /// </summary>
    public static TheoryData<ulong, int, ulong[]> ReferenceVectors => new()
    {
        {
            0UL, 0,
            [0x2d9463cdb2e25574, 0x1db29faf46472f59, 0x679c51cbbe04d321, 0xd01bf2a6441c84d4, 0xcebfc39805ea62a0]
        },
        {
            1UL, 1,
            [0x03c89af7e23d2b85, 0x00db683a3d418191, 0x078026b705d5f477, 0xb8235a82b481641e, 0x578e7af9e65483ce]
        },
        {
            42UL, 7,
            [0x3d055501d44d97fa, 0x325d6e22caf761a0, 0x42bf7851f9e22cd5, 0xc730e3481863785a, 0x9118c77f16d292a1]
        },
        {
            0xDEADBEEFUL, -1,
            [0x24d51f3bce40c1e5, 0x8f08cdf26e95e7cb, 0xd9f5ecbb246ba9da, 0x014c2b2b1289c6cb, 0xf9eb846613c7f22b]
        },
    };

    [Theory]
    [MemberData(nameof(ReferenceVectors))]
    public void NextUInt64_MatchesReferenceImplementation(ulong seed, int run, ulong[] expected)
    {
        var random = Xoshiro256StarStar.ForRun(seed, run);

        var actual = new ulong[expected.Length];
        for (var i = 0; i < actual.Length; i++)
        {
            actual[i] = random.NextUInt64();
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData((1UL << 32) - 1)]
    [InlineData(1UL << 32)]
    [InlineData((1UL << 32) + 1)]
    [InlineData((1UL << 63) - 1)]
    [InlineData(1UL << 63)]
    [InlineData(ulong.MaxValue - 1)]
    [InlineData(ulong.MaxValue)]
    public void NextUInt64Inclusive_NeverExceedsMax(ulong max)
    {
        var random = Xoshiro256StarStar.ForRun(seed: 12345, run: 0);

        for (var i = 0; i < 1_000; i++)
        {
            Assert.InRange(random.NextUInt64Inclusive(max), 0UL, max);
        }
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(7UL)]
    [InlineData(8UL)]
    public void NextUInt64Inclusive_ReachesEveryValueInRange(ulong max)
    {
        var random = Xoshiro256StarStar.ForRun(seed: 12345, run: 1);

        var seen = new HashSet<ulong>();
        for (var i = 0; i < 1_000; i++)
        {
            seen.Add(random.NextUInt64Inclusive(max));
        }

        var expected = Enumerable.Range(0, (int)max + 1).Select(v => (ulong)v);
        Assert.Equal(expected, seen.Order());
    }
}
