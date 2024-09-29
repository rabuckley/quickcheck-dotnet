using System.Collections;
using System.Numerics;

namespace QuickCheck.Generators;

public class ArbitraryUnsignedIntegerGenerator<T> : ArbitraryValueGenerator<T>
    where T : IUnsignedNumber<T>, IMinMaxValue<T>, IBinaryInteger<T>
{
    public ArbitraryUnsignedIntegerGenerator() : base(Random.Shared)
    {
    }

    public ArbitraryUnsignedIntegerGenerator(Random random) : base(random)
    {
    }

    public override T Generate()
    {
        // Weight `MinValue`, `One`, and `MaxValue` higher because they're likely edge cases. 
        return Random.Next(0, 10) switch
        {
            0 => Choose([T.MinValue, T.One, T.MaxValue]),
            _ => BinaryNumberSampler.NextUnsigned(T.MinValue, T.MaxValue)
        };
    }

    public override IEnumerable<T> Shrink(T from)
    {
        return UnsignedIntegerShrinker.Create(from);
    }

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

public sealed class ArbitraryByteGenerator : ArbitraryUnsignedIntegerGenerator<byte>
{
    public static readonly ArbitraryByteGenerator Default = new();
}

public sealed class ArbitraryUInt16Generator : ArbitraryUnsignedIntegerGenerator<ushort>
{
    public static readonly ArbitraryUInt16Generator Default = new();
}

public sealed class ArbitraryUInt32Generator : ArbitraryUnsignedIntegerGenerator<uint>
{
    public static readonly ArbitraryUInt32Generator Default = new();
}

public sealed class ArbitraryUInt64Generator : ArbitraryUnsignedIntegerGenerator<ulong>
{
    public static readonly ArbitraryUInt64Generator Default = new();
}