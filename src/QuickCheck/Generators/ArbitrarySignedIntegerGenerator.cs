using System.Collections;
using System.IO.Compression;
using System.Numerics;

namespace QuickCheck.Generators;

public class ArbitrarySignedIntegerGenerator<T> : ArbitraryValueGenerator<T>
    where T : ISignedNumber<T>, IMinMaxValue<T>, IBinaryInteger<T>
{
    public ArbitrarySignedIntegerGenerator() : base(Random.Shared)
    {
    }

    public ArbitrarySignedIntegerGenerator(Random random) : base(random)
    {
    }

    public override T Generate()
    {
        // Weight `MinValue`, `Zero`, and `MaxValue` higher because they're likely edge cases.
        return Random.Next(0, 10) switch
        {
            0 => Choose([T.MinValue, T.Zero, T.MaxValue]),
            _ => BinaryNumberSampler.NextSigned(T.MinValue, T.MaxValue)
        };
    }

    public override IEnumerable<T> Shrink(T from)
    {
        return SignedIntegerShrinker.Create(from);
    }

    private sealed class SignedIntegerShrinker : IEnumerator<T>, IEnumerable<T>
    {
        private T _current;
        private readonly T _from;
        private T _diff;

        private SignedIntegerShrinker(T from)
        {
            _from = from;
            _diff = from / T.CreateChecked(2);
            _current = default!;
        }

        public static IEnumerable<T> Create(T from)
        {
            if (from == T.Zero)
            {
                return [];
            }

            var shrinker = new SignedIntegerShrinker(from);

            List<T> items = [T.Zero];

            if (shrinker._diff < T.Zero && shrinker._from != T.MinValue)
            {
                // If half is negative and current is not the max value, add the positive value of current.
                items.Add(T.Abs(shrinker._from));
            }

            return items.Concat(shrinker);
        }

        public bool MoveNext()
        {
            if (_from == T.MinValue || T.Abs(_from - _diff) < T.Abs(_from))
            {
                _current = _from - _diff;
                _diff /= T.CreateChecked(2);
                return true;
            }

            // No smaller to go
            return false;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        T IEnumerator<T>.Current => _current;

        object IEnumerator.Current => _current;

        public IEnumerator<T> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this;
        }

        public void Dispose()
        {
        }
    }
}


/// <summary>
/// A pseudo-random generator for <see cref="Int16"/> values.
/// </summary>
public sealed class ArbitraryInt16Generator : ArbitrarySignedIntegerGenerator<short>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryInt16Generator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryInt16Generator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryInt16Generator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryInt16Generator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new <see cref="ArbitraryInt16Generator"/> with the specified <paramref name="random"/> pseudo-random number generator.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryInt16Generator(Random random) : base(random)
    {
    }
}

/// <summary>
/// A pseudo-random generator for <see cref="Int32"/> values.
/// </summary>
public sealed class ArbitraryInt32Generator : ArbitrarySignedIntegerGenerator<int>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryInt32Generator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryInt32Generator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryInt32Generator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryInt32Generator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new <see cref="ArbitraryInt32Generator"/> with the specified <paramref name="random"/> pseudo-random number generator.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryInt32Generator(Random random) : base(random)
    {
    }
}

/// <summary>
/// A pseudo-random generator for <see cref="Int64"/> values.
/// </summary>
public sealed class ArbitraryInt64Generator : ArbitrarySignedIntegerGenerator<long>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryInt64Generator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryInt64Generator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryInt64Generator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryInt64Generator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new <see cref="ArbitraryInt64Generator"/> with the specified <paramref name="random"/> pseudo-random number generator.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryInt64Generator(Random random) : base(random)
    {
    }
}
