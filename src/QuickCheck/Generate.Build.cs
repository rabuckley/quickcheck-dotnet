namespace QuickCheck;

public static partial class Generate
{
    /// <summary>
    /// Creates a generator that draws one value from each of two generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/> or <paramref name="construct"/>
    /// is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Func<T1, T2, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(source.Draw(generator1), source.Draw(generator2)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of three generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="generator3">The generator that produces the third value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/>
    /// or <paramref name="construct"/> is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, T3, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Func<T1, T2, T3, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(source.Draw(generator1), source.Draw(generator2), source.Draw(generator3)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of four generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="generator3">The generator that produces the third value.</param>
    /// <param name="generator4">The generator that produces the fourth value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/>,
    /// <paramref name="generator4"/> or <paramref name="construct"/> is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, T3, T4, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Generator<T4> generator4,
        Func<T1, T2, T3, T4, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);
        ArgumentNullException.ThrowIfNull(generator4);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(
            source.Draw(generator1),
            source.Draw(generator2),
            source.Draw(generator3),
            source.Draw(generator4)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of five generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="generator3">The generator that produces the third value.</param>
    /// <param name="generator4">The generator that produces the fourth value.</param>
    /// <param name="generator5">The generator that produces the fifth value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/>,
    /// <paramref name="generator4"/>, <paramref name="generator5"/> or <paramref name="construct"/>
    /// is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, T3, T4, T5, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Generator<T4> generator4,
        Generator<T5> generator5,
        Func<T1, T2, T3, T4, T5, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);
        ArgumentNullException.ThrowIfNull(generator4);
        ArgumentNullException.ThrowIfNull(generator5);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(
            source.Draw(generator1),
            source.Draw(generator2),
            source.Draw(generator3),
            source.Draw(generator4),
            source.Draw(generator5)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of six generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="generator3">The generator that produces the third value.</param>
    /// <param name="generator4">The generator that produces the fourth value.</param>
    /// <param name="generator5">The generator that produces the fifth value.</param>
    /// <param name="generator6">The generator that produces the sixth value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/>,
    /// <paramref name="generator4"/>, <paramref name="generator5"/>, <paramref name="generator6"/>
    /// or <paramref name="construct"/> is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, T3, T4, T5, T6, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Generator<T4> generator4,
        Generator<T5> generator5,
        Generator<T6> generator6,
        Func<T1, T2, T3, T4, T5, T6, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);
        ArgumentNullException.ThrowIfNull(generator4);
        ArgumentNullException.ThrowIfNull(generator5);
        ArgumentNullException.ThrowIfNull(generator6);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(
            source.Draw(generator1),
            source.Draw(generator2),
            source.Draw(generator3),
            source.Draw(generator4),
            source.Draw(generator5),
            source.Draw(generator6)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of seven generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="generator3">The generator that produces the third value.</param>
    /// <param name="generator4">The generator that produces the fourth value.</param>
    /// <param name="generator5">The generator that produces the fifth value.</param>
    /// <param name="generator6">The generator that produces the sixth value.</param>
    /// <param name="generator7">The generator that produces the seventh value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/>,
    /// <paramref name="generator4"/>, <paramref name="generator5"/>, <paramref name="generator6"/>,
    /// <paramref name="generator7"/> or <paramref name="construct"/> is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, T3, T4, T5, T6, T7, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Generator<T4> generator4,
        Generator<T5> generator5,
        Generator<T6> generator6,
        Generator<T7> generator7,
        Func<T1, T2, T3, T4, T5, T6, T7, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);
        ArgumentNullException.ThrowIfNull(generator4);
        ArgumentNullException.ThrowIfNull(generator5);
        ArgumentNullException.ThrowIfNull(generator6);
        ArgumentNullException.ThrowIfNull(generator7);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(
            source.Draw(generator1),
            source.Draw(generator2),
            source.Draw(generator3),
            source.Draw(generator4),
            source.Draw(generator5),
            source.Draw(generator6),
            source.Draw(generator7)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of eight generators, in order, and
    /// combines them with a function.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <typeparam name="T8">The type of the eighth value.</typeparam>
    /// <typeparam name="TResult">The type of value returned by <paramref name="construct"/>.</typeparam>
    /// <param name="generator1">The generator that produces the first value.</param>
    /// <param name="generator2">The generator that produces the second value.</param>
    /// <param name="generator3">The generator that produces the third value.</param>
    /// <param name="generator4">The generator that produces the fourth value.</param>
    /// <param name="generator5">The generator that produces the fifth value.</param>
    /// <param name="generator6">The generator that produces the sixth value.</param>
    /// <param name="generator7">The generator that produces the seventh value.</param>
    /// <param name="generator8">The generator that produces the eighth value.</param>
    /// <param name="construct">The function that builds the result from the drawn values.</param>
    /// <returns>
    /// A generator that produces the values <paramref name="construct"/> returns, and that shrinks as
    /// the drawn values shrink.
    /// </returns>
    /// <remarks>
    /// <para>
    /// A <see cref="DiscardException"/> thrown by <paramref name="construct"/> discards the example;
    /// any other exception fails the check with that exception.
    /// </para>
    /// <para>
    /// The values are drawn independently of one another. To generate values that a relation ties
    /// together, such as a lower bound and an upper bound, filter the tuple before building
    /// (<c>Generate.Tuple(low, high).Where(pair => pair.Item1 &lt;= pair.Item2).Select(...)</c>) or throw
    /// <see cref="DiscardException"/> from <paramref name="construct"/>.
    /// </para>
    /// <para>
    /// For one value, use <see cref="Select{T, TResult}(Generator{T}, Func{T, TResult})"/>; for more than
    /// eight, nest a <c>Build</c> or use <see cref="Sequence{T}(IEnumerable{Generator{T}})"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/>,
    /// <paramref name="generator4"/>, <paramref name="generator5"/>, <paramref name="generator6"/>,
    /// <paramref name="generator7"/>, <paramref name="generator8"/> or <paramref name="construct"/>
    /// is <see langword="null"/>.
    /// </exception>
    public static Generator<TResult> Build<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Generator<T4> generator4,
        Generator<T5> generator5,
        Generator<T6> generator6,
        Generator<T7> generator7,
        Generator<T8> generator8,
        Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> construct)
    {
        ArgumentNullException.ThrowIfNull(generator1);
        ArgumentNullException.ThrowIfNull(generator2);
        ArgumentNullException.ThrowIfNull(generator3);
        ArgumentNullException.ThrowIfNull(generator4);
        ArgumentNullException.ThrowIfNull(generator5);
        ArgumentNullException.ThrowIfNull(generator6);
        ArgumentNullException.ThrowIfNull(generator7);
        ArgumentNullException.ThrowIfNull(generator8);
        ArgumentNullException.ThrowIfNull(construct);

        return From(source => construct(
            source.Draw(generator1),
            source.Draw(generator2),
            source.Draw(generator3),
            source.Draw(generator4),
            source.Draw(generator5),
            source.Draw(generator6),
            source.Draw(generator7),
            source.Draw(generator8)));
    }

    /// <summary>
    /// Creates a generator that draws one value from each of the specified generators, in order, and
    /// produces them as an array.
    /// </summary>
    /// <typeparam name="T">The type of the values.</typeparam>
    /// <param name="generators">
    /// The generators to draw from. The sequence is enumerated once, when this method is called.
    /// </param>
    /// <returns>
    /// A generator that produces a new array on every draw, holding one value per generator in the
    /// same order, and that shrinks each element independently. When <paramref name="generators"/> is
    /// empty, the array is empty and no choices are consumed.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generators"/> or an element of it is <see langword="null"/>.
    /// </exception>
    public static Generator<T[]> Sequence<T>(params IEnumerable<Generator<T>> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);

        var array = generators.ToArray();

        foreach (var generator in array)
        {
            ArgumentNullException.ThrowIfNull(generator, nameof(generators));
        }

        return From(source =>
        {
            var values = new T[array.Length];

            for (var i = 0; i < array.Length; i++)
            {
                values[i] = source.Draw(array[i]);
            }

            return values;
        });
    }
}
