using QuickCheck.Generators;

namespace QuickCheck;

public static partial class Generate
{
    /// <summary>
    /// The number of candidates a filtering generator draws before discarding
    /// the example. Kept small: a predicate that rejects this often should be
    /// expressed as a generator that only produces valid values.
    /// </summary>
    internal const int MaxFilterAttempts = 10;

    extension<T>(Generator<T> generator)
    {
        /// <summary>
        /// Projects each generated value into a new form.
        /// </summary>
        /// <typeparam name="TResult">The type of value returned by <paramref name="selector"/>.</typeparam>
        /// <param name="selector">The transform to apply to each generated value.</param>
        /// <returns>
        /// A generator whose values are the transformed values of the source generator, and which
        /// shrinks as the source generator shrinks.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="selector"/> is <see langword="null"/>.</exception>
        public Generator<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentNullException.ThrowIfNull(selector);
            return From(source => selector(source.Draw(generator)));
        }

        /// <summary>
        /// Filters the generated values based on a predicate.
        /// </summary>
        /// <param name="predicate">The condition a generated value is required to satisfy.</param>
        /// <returns>
        /// A generator whose values satisfy <paramref name="predicate"/>. It draws up to
        /// <see cref="MaxFilterAttempts"/> candidates per example and discards the example if none of
        /// them passes.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
        public Generator<T> Where(Func<T, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentNullException.ThrowIfNull(predicate);

            return From(source =>
            {
                for (var attempt = 0; attempt < MaxFilterAttempts; attempt++)
                {
                    var value = source.Draw(generator);

                    if (predicate(value))
                    {
                        return value;
                    }
                }

                throw new DiscardException($"No value satisfied the filter after {MaxFilterAttempts} attempts.");
            });
        }

        /// <summary>
        /// Draws a value and then draws from the generator that <paramref name="binder"/> builds from
        /// it, for values whose generator depends on an earlier value.
        /// </summary>
        /// <typeparam name="TResult">The type of value produced by the second generator.</typeparam>
        /// <param name="binder">The function that builds the second generator from the first value.</param>
        /// <returns>A generator that produces the value drawn from the second generator.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="binder"/> is <see langword="null"/>.</exception>
        public Generator<TResult> SelectMany<TResult>(Func<T, Generator<TResult>> binder)
        {
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentNullException.ThrowIfNull(binder);
            return From(source => source.Draw(binder(source.Draw(generator))));
        }

        /// <summary>
        /// Draws a value, draws from the generator that <paramref name="binder"/> builds from it, and
        /// combines the two with <paramref name="projector"/>. This is the overload a LINQ query of the
        /// form <c>from a in genA from b in f(a) select g(a, b)</c> binds to.
        /// </summary>
        /// <typeparam name="TMiddle">The type of value produced by the second generator.</typeparam>
        /// <typeparam name="TResult">The type of value returned by <paramref name="projector"/>.</typeparam>
        /// <param name="binder">The function that builds the second generator from the first value.</param>
        /// <param name="projector">The function that combines the two values.</param>
        /// <returns>A generator that produces the combined values.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="binder"/> or <paramref name="projector"/> is <see langword="null"/>.
        /// </exception>
        public Generator<TResult> SelectMany<TMiddle, TResult>(
            Func<T, Generator<TMiddle>> binder,
            Func<T, TMiddle, TResult> projector)
        {
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentNullException.ThrowIfNull(binder);
            ArgumentNullException.ThrowIfNull(projector);

            return From(source =>
            {
                var first = source.Draw(generator);
                var second = source.Draw(binder(first));
                return projector(first, second);
            });
        }

        /// <summary>
        /// Creates a generator for lists whose elements are drawn from this generator.
        /// </summary>
        /// <param name="minLength">The inclusive lower bound of the list length. The default is 0.</param>
        /// <param name="maxLength">The inclusive upper bound of the list length. The default is 64.</param>
        /// <returns>
        /// A generator that produces lists within the given length range, and that shrinks by removing
        /// elements and shrinking the ones that remain.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="minLength"/> is negative, or <paramref name="maxLength"/> is less than
        /// <paramref name="minLength"/>.
        /// </exception>
        public Generator<List<T>> List(int minLength = 0, int maxLength = 64) =>
            new ListGenerator<T>(generator, minLength, maxLength);

        /// <summary>
        /// Creates a generator for arrays whose elements are drawn from this generator.
        /// </summary>
        /// <param name="minLength">The inclusive lower bound of the array length. The default is 0.</param>
        /// <param name="maxLength">The inclusive upper bound of the array length. The default is 64.</param>
        /// <returns>A generator that produces arrays within the given length range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="minLength"/> is negative, or <paramref name="maxLength"/> is less than
        /// <paramref name="minLength"/>.
        /// </exception>
        public Generator<T[]> Array(int minLength = 0, int maxLength = 64) =>
            new ListGenerator<T>(generator, minLength, maxLength).Select(static list => list.ToArray());

        /// <summary>
        /// Creates a generator for <see cref="Memory{T}"/> whose elements are drawn from this
        /// generator.
        /// </summary>
        /// <param name="minLength">The inclusive lower bound of the length. The default is 0.</param>
        /// <param name="maxLength">The inclusive upper bound of the length. The default is 64.</param>
        /// <returns>A generator that produces memory within the given length range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="minLength"/> is negative, or <paramref name="maxLength"/> is less than
        /// <paramref name="minLength"/>.
        /// </exception>
        public Generator<Memory<T>> Memory(int minLength = 0, int maxLength = 64) =>
            generator.Array(minLength, maxLength).Select(static array => new Memory<T>(array));
    }

    extension<T>(Generator<T> generator) where T : class
    {
        /// <summary>
        /// Creates a generator that produces <see langword="null"/> with a given probability and
        /// otherwise draws from this generator.
        /// </summary>
        /// <param name="nullProbability">
        /// The probability of producing <see langword="null"/>, from 0 to 1. The default is 0.1.
        /// </param>
        /// <returns>
        /// A generator that produces <see langword="null"/> or a generated value, and that shrinks
        /// towards <see langword="null"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="nullProbability"/> is less than 0 or greater than 1.
        /// </exception>
        public Generator<T?> OrNull(double nullProbability = 0.1)
        {
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentOutOfRangeException.ThrowIfLessThan(nullProbability, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(nullProbability, 1);

            return From<T?>(source =>
                source.NextBoolean(1 - nullProbability) ? source.Draw(generator) : null);
        }
    }

    extension<T>(Generator<T> generator) where T : struct
    {
        /// <summary>
        /// Creates a generator for <see cref="Nullable{T}"/> that produces <see langword="null"/> with
        /// a given probability and otherwise draws from this generator.
        /// </summary>
        /// <param name="nullProbability">
        /// The probability of producing <see langword="null"/>, from 0 to 1. The default is 0.1.
        /// </param>
        /// <returns>
        /// A generator that produces <see langword="null"/> or a generated value, and that shrinks
        /// towards <see langword="null"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="nullProbability"/> is less than 0 or greater than 1.
        /// </exception>
        public Generator<T?> Nullable(double nullProbability = 0.1)
        {
            ArgumentNullException.ThrowIfNull(generator);
            ArgumentOutOfRangeException.ThrowIfLessThan(nullProbability, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(nullProbability, 1);

            return From<T?>(source =>
                source.NextBoolean(1 - nullProbability) ? source.Draw(generator) : null);
        }
    }
}
