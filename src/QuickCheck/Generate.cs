using System.Numerics;
using QuickCheck.Choices;
using QuickCheck.Generators;

namespace QuickCheck;

/// <summary>
/// Provides static methods for creating <see cref="Generator{T}"/> instances and for composing them.
/// </summary>
public static partial class Generate
{
    /// <summary>
    /// Creates a generator from a function that draws from a <see cref="ChoiceSource"/>.
    /// </summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="generate">
    /// The function that produces a value. It must draw all randomness from the source it is given
    /// and may be called concurrently; see <see cref="Generator{T}.Generate"/>.
    /// </param>
    /// <returns>A generator that produces values by calling <paramref name="generate"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="generate"/> is <see langword="null"/>.</exception>
    public static Generator<T> From<T>(Func<ChoiceSource, T> generate)
    {
        ArgumentNullException.ThrowIfNull(generate);
        return new DelegateGenerator<T>(generate);
    }

    /// <summary>
    /// Creates a generator that always produces the same value.
    /// </summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="value">The value to produce.</param>
    /// <returns>A generator that produces <paramref name="value"/> and consumes no choices.</returns>
    public static Generator<T> Constant<T>(T value) =>
        new ConstantGenerator<T>(value);

    /// <summary>
    /// Creates a generator that defers construction of the underlying generator until it is first
    /// used, which allows a recursive generator to refer to itself.
    /// </summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="factory">The function that creates the underlying generator.</param>
    /// <returns>A generator that draws from the generator <paramref name="factory"/> returns.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is <see langword="null"/>.</exception>
    public static Generator<T> Deferred<T>(Func<Generator<T>> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new DeferredGenerator<T>(factory);
    }

    /// <summary>
    /// Creates a generator for integers over the full range of <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The integer type to generate, whose range must span at most 64 bits.</typeparam>
    /// <returns>A generator that produces values of <typeparamref name="T"/> and shrinks towards zero.</returns>
    /// <exception cref="NotSupportedException">
    /// The range of <typeparamref name="T"/> spans more than 64 bits.
    /// </exception>
    public static Generator<T> Integer<T>() where T : IBinaryInteger<T>, IMinMaxValue<T>
    {
        if (!IntegerRange<T>.TryGetDistance(T.MinValue, T.MaxValue, out _))
        {
            throw new NotSupportedException(
                $"{typeof(T).Name} spans more than 64 bits; use Between to generate a narrower range.");
        }

        return Between(T.MinValue, T.MaxValue);
    }

    /// <summary>
    /// Creates a generator for integers within a specified range.
    /// </summary>
    /// <typeparam name="T">The integer type to generate.</typeparam>
    /// <param name="min">The inclusive lower bound of the values to generate.</param>
    /// <param name="max">The inclusive upper bound of the values to generate.</param>
    /// <returns>
    /// A generator that produces values from <paramref name="min"/> to <paramref name="max"/> and
    /// shrinks towards zero, or towards the bound nearest zero when the range excludes it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="min"/> is greater than <paramref name="max"/>, or the range spans more than 64
    /// bits.
    /// </exception>
    public static Generator<T> Between<T>(T min, T max) where T : IBinaryInteger<T> =>
        new IntegerGenerator<T>(min, max);

    /// <summary>
    /// Creates a generator for booleans.
    /// </summary>
    /// <returns>
    /// A generator that produces <see langword="true"/> and <see langword="false"/> with equal
    /// probability and shrinks towards <see langword="false"/>.
    /// </returns>
    public static Generator<bool> Boolean() => BooleanGenerator.Instance;

    /// <summary>
    /// Creates a generator for characters.
    /// </summary>
    /// <returns>
    /// A generator that produces any UTF-16 code unit, biased towards printable ASCII, and shrinks
    /// towards <c>'a'</c>.
    /// </returns>
    public static Generator<char> Char() => CharacterGenerator.Any;

    /// <summary>
    /// Creates a generator for strings of <see cref="Char()"/> characters.
    /// </summary>
    /// <param name="minLength">The inclusive lower bound of the string length. The default is 0.</param>
    /// <param name="maxLength">The inclusive upper bound of the string length. The default is 64.</param>
    /// <returns>
    /// A generator that produces strings within the given length range and shrinks towards a string of
    /// <paramref name="minLength"/> characters.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minLength"/> is negative, or <paramref name="maxLength"/> is less than
    /// <paramref name="minLength"/>.
    /// </exception>
    public static Generator<string> String(
        int minLength = 0,
        int maxLength = 64) =>
        String(Char(), minLength, maxLength);

    /// <summary>
    /// Creates a generator for strings whose characters are drawn from a specified generator.
    /// </summary>
    /// <param name="chars">The generator that produces the characters of each string.</param>
    /// <param name="minLength">The inclusive lower bound of the string length. The default is 0.</param>
    /// <param name="maxLength">The inclusive upper bound of the string length. The default is 64.</param>
    /// <returns>
    /// A generator that produces strings within the given length range and shrinks towards a string of
    /// <paramref name="minLength"/> characters.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="chars"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minLength"/> is negative, or <paramref name="maxLength"/> is less than
    /// <paramref name="minLength"/>.
    /// </exception>
    public static Generator<string> String(
        Generator<char> chars,
        int minLength = 0,
        int maxLength = 64)
    {
        ArgumentNullException.ThrowIfNull(chars);

        return chars.Array(minLength, maxLength).Select(static array => new string(array));
    }

    /// <summary>
    /// Creates a generator that picks uniformly from the specified items.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="items">
    /// The items to pick from, as separate arguments or as one sequence. A single <see cref="string"/>
    /// argument is a sequence of characters.
    /// </param>
    /// <returns>
    /// A generator that produces one of <paramref name="items"/> and shrinks towards the first of
    /// them.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="items"/> is empty.</exception>
    public static Generator<T> Elements<T>(params IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var array = items.ToArray();

        return array.Length == 0
            ? throw new ArgumentException("At least one item is required.", nameof(items))
            : new ElementsGenerator<T>(array);
    }

    /// <summary>
    /// Creates a generator for the values of an enumeration.
    /// </summary>
    /// <typeparam name="T">The enumeration type to generate.</typeparam>
    /// <returns>
    /// A generator that produces one of the values of <typeparamref name="T"/> and shrinks towards the
    /// first declared value.
    /// </returns>
    public static Generator<T> Enum<T>() where T : struct, Enum => Elements(System.Enum.GetValues<T>());

    /// <summary>
    /// Creates a generator that draws from one of the specified generators, chosen uniformly.
    /// </summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="generators">
    /// The generators to choose between, as separate arguments or as one sequence.
    /// </param>
    /// <returns>
    /// A generator that draws from one of <paramref name="generators"/> and shrinks towards the first
    /// of them.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generators"/> or an element of it is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="generators"/> is empty.</exception>
    public static Generator<T> OneOf<T>(params IEnumerable<Generator<T>> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);

        var weighted = generators.Select(static generator => (Weight: 1, Generator: generator)).ToArray();

        if (weighted.Length == 0)
        {
            throw new ArgumentException("At least one generator is required.", nameof(generators));
        }

        foreach (var (_, generator) in weighted)
        {
            ArgumentNullException.ThrowIfNull(generator, nameof(generators));
        }

        return new FrequencyGenerator<T>(weighted);
    }

    /// <summary>
    /// Creates a generator that draws from one of the specified generators, choosing each with a
    /// probability proportional to its weight.
    /// </summary>
    /// <typeparam name="T">The type of value to generate.</typeparam>
    /// <param name="weightedGenerators">
    /// The generators to choose between, each paired with its weight, as separate arguments or as one
    /// sequence.
    /// </param>
    /// <returns>
    /// A generator that draws from one of <paramref name="weightedGenerators"/> and shrinks towards
    /// the first of them.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="weightedGenerators"/> or a generator in it is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="weightedGenerators"/> is empty, or a weight is less than or equal to zero.
    /// </exception>
    public static Generator<T> Frequency<T>(
        params IEnumerable<(int Weight, Generator<T> Generator)> weightedGenerators)
    {
        ArgumentNullException.ThrowIfNull(weightedGenerators);

        var weighted = weightedGenerators.ToArray();

        if (weighted.Length == 0)
        {
            throw new ArgumentException("At least one generator is required.", nameof(weightedGenerators));
        }

        foreach (var (weight, generator) in weighted)
        {
            ArgumentNullException.ThrowIfNull(generator, nameof(weightedGenerators));

            if (weight <= 0)
            {
                throw new ArgumentException("Weights must be positive.", nameof(weightedGenerators));
            }
        }

        return new FrequencyGenerator<T>(weighted);
    }

    /// <summary>
    /// Creates a generator for pairs of values drawn independently from two generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each pair.</param>
    /// <param name="generator2">The generator that produces the second value of each pair.</param>
    /// <returns>A generator that produces pairs of values.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/> or <paramref name="generator2"/> is <see langword="null"/>.
    /// </exception>
    public static Generator<(T1, T2)> Tuple<T1, T2>(
        Generator<T1> generator1,
        Generator<T2> generator2)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);

        return From(source => (source.Draw(generator1), source.Draw(generator2)));
    }

    /// <summary>
    /// Creates a generator for triples of values drawn independently from three generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each triple.</param>
    /// <param name="generator2">The generator that produces the second value of each triple.</param>
    /// <param name="generator3">The generator that produces the third value of each triple.</param>
    /// <returns>A generator that produces triples of values.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/> or <paramref name="generator3"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Generator<(T1, T2, T3)> Tuple<T1, T2, T3>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);

        return From(source => (source.Draw(generator1), source.Draw(generator2), source.Draw(generator3)));
    }
}
