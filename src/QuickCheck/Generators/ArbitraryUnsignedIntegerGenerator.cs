using System.Collections;
using System.Numerics;

namespace QuickCheck.Generators;


/// <summary>
/// A pseudo-random number generator for unsigned binary integer values.
/// </summary>
/// <typeparam name="T">The type to generate.</typeparam>
public class ArbitraryUnsignedIntegerGenerator<T> : ArbitraryValueGenerator<T>
    where T : IUnsignedNumber<T>, IMinMaxValue<T>, IBinaryInteger<T>
{
    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryUnsignedIntegerGenerator{T}"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryUnsignedIntegerGenerator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryUnsignedIntegerGenerator{T}"/> with the specified <paramref name="random"/> pseudo-random number generator.
    /// </summary>
    /// <param name="random"></param>
    public ArbitraryUnsignedIntegerGenerator(Random random) : base(random)
    {
    }

    /// <inheritdoc />
    public override T Generate()
    {
        // Weight `MinValue`, `One`, and `MaxValue` higher because they're likely edge cases.
        return Random.Next(0, 10) switch
        {
            0 => Choose([T.MinValue, T.One, T.MaxValue]),
            _ => BinaryNumberSampler.NextUnsigned(T.MinValue, T.MaxValue)
        };
    }

    /// <inheritdoc />
    public override IEnumerable<T> Shrink(T from)
    {
        return UnsignedIntegerShrinker.Create(from);
    }

    /// <summary>
    /// An enumerator that shrinks an arbitrary unsigned integer value.
    /// </summary>
    public sealed class UnsignedIntegerShrinker : IEnumerator<T>, IEnumerable<T>
    {
        private T _current;
        private T _from;
        private T _diff;

        private UnsignedIntegerShrinker(T from)
        {
            _from = from;
            _diff = from / T.CreateChecked(2);
            _current = default!;
        }

        public static IEnumerable<T> Create(T from)
        {
            if (from == T.Zero)
            {
                // Can't get any smaller.
                return [];
            }

            // Manually add zero.
            T[] a = [T.Zero];
            return a.Concat(new UnsignedIntegerShrinker(from));
        }

        public bool MoveNext()
        {
            if (_from - _diff < _from)
            {
                _current = _from - _diff;
                _from -= _diff;
                _diff /= T.CreateChecked(2);
                return true;
            }

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
/// A pseudo-random number generator for <see cref="Byte"/> values.
/// </summary>
public sealed class ArbitraryByteGenerator : ArbitraryUnsignedIntegerGenerator<byte>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryByteGenerator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryByteGenerator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryByteGenerator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryByteGenerator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryByteGenerator"/> with the specified <paramref name="random"/> instance.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryByteGenerator(Random random) : base(random)
    {
    }
}

/// <summary>
/// A pseudo-random number generator for <see cref="UInt16"/> values.
/// </summary>
public sealed class ArbitraryUInt16Generator : ArbitraryUnsignedIntegerGenerator<ushort>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryUInt16Generator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryUInt16Generator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryUInt16Generator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryUInt16Generator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryUInt16Generator"/> with the specified <paramref name="random"/> instance.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryUInt16Generator(Random random) : base(random)
    {
    }
}

/// <summary>
/// A pseudo-random number generator for <see cref="UInt32"/> values.
/// </summary>
public sealed class ArbitraryUInt32Generator : ArbitraryUnsignedIntegerGenerator<uint>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryUInt32Generator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryUInt32Generator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryUInt32Generator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryUInt32Generator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryUInt32Generator"/> with the specified <paramref name="random"/> instance.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryUInt32Generator(Random random) : base(random)
    {
    }
}

/// <summary>
/// A pseudo-random number generator for <see cref="UInt64"/> values.
/// </summary>
public sealed class ArbitraryUInt64Generator : ArbitraryUnsignedIntegerGenerator<ulong>
{
    /// <summary>
    /// Gets a read-only singleton instance of <see cref="ArbitraryUInt64Generator"/> using the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public static readonly ArbitraryUInt64Generator Default = new();

    /// <summary>
    /// Creates a new <see cref="ArbitraryUInt64Generator"/> with the default <see cref="Random.Shared"/> pseudo-random number generator.
    /// </summary>
    public ArbitraryUInt64Generator() : base(Random.Shared)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ArbitraryUInt64Generator"/> with the specified <paramref name="random"/> instance.
    /// </summary>
    /// <param name="random">The pseudo-random number generator to use.</param>
    public ArbitraryUInt64Generator(Random random) : base(random)
    {
    }
}
