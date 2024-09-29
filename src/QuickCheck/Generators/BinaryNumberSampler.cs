using System.Buffers;
using System.Numerics;

namespace QuickCheck.Generators;

internal static class BinaryNumberSampler
{
    /// <summary>
    /// Gets a random unsigned binary integer in the provided range.
    /// </summary>
    /// <param name="min">The inclusive lower bound</param>
    /// <param name="max">The exclusive upper bound.</param>
    public static T NextUnsigned<T>(T min, T max) where T : IBinaryInteger<T>, IUnsignedNumber<T>
    {
        return Next(min, max, isUnsigned: true);
    }

    /// <summary>
    /// Gets a random signed binary integer in the provided range.
    /// </summary>
    /// <param name="min">The inclusive lower bound</param>
    /// <param name="max">The exclusive upper bound.</param>
    public static T NextSigned<T>(T min, T max) where T : IBinaryInteger<T>, ISignedNumber<T>
    {
        return Next(min, max, isUnsigned: false);
    }

    /// <summary>
    /// Gets a random number in the provided range
    /// </summary>
    /// <param name="min">The inclusive lower bound</param>
    /// <param name="max">The exclusive upper bound.</param>
    /// <param name="isUnsigned"></param>
    /// <typeparam name="T"></typeparam>
    private static T Next<T>(T min, T max, bool isUnsigned) where T : IBinaryInteger<T>
    {
        var maxBytes = (int)Math.Ceiling(max.GetShortestBitLength() / 8.0);

        byte[]? rented = null;

        Span<byte> span = maxBytes <= 256
            ? stackalloc byte[maxBytes]
            : (rented = ArrayPool<byte>.Shared.Rent(maxBytes));

        try
        {
            while (true)
            {
                Random.Shared.NextBytes(span);
                var candidate = T.ReadLittleEndian(span, isUnsigned);

                if (min <= candidate && candidate < max)
                {
                    return candidate;
                }
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}