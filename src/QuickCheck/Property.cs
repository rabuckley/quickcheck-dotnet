using System.Runtime.CompilerServices;

namespace QuickCheck;

/// <summary>
/// Provides static methods for stating properties over generated values.
/// </summary>
public static class Property
{
    /// <summary>
    /// Discards the current example unless <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">The condition the example is required to satisfy.</param>
    /// <exception cref="DiscardException"><paramref name="condition"/> is <see langword="false"/>.</exception>
    /// <remarks>
    /// A discarded example is replaced by a newly generated one, so discarding still costs generation
    /// time, and a property that discards most of its examples ends as
    /// <see cref="PropertyOutcome.Exhausted"/>; prefer a generator that produces only valid inputs. The
    /// property body must let the <see cref="DiscardException"/> propagate, because a
    /// <see langword="catch"/> clause that swallows it turns a discarded example into a passing one.
    /// </remarks>
    public static void Assume(bool condition)
    {
        if (!condition)
        {
            throw new DiscardException("An assumption did not hold.");
        }
    }

    /// <summary>
    /// Creates a property that holds when <paramref name="body"/> returns without throwing for every
    /// value produced by <paramref name="generator"/>.
    /// </summary>
    /// <typeparam name="T">The type of value the property is checked over.</typeparam>
    /// <param name="generator">The generator that produces the examples.</param>
    /// <param name="body">The action to run on each example.</param>
    /// <returns>
    /// A property that can be checked with <see cref="Property{T}.Assert"/> or
    /// <see cref="Property{T}.Check"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator"/> or <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    public static Property<T> ForAll<T>(Generator<T> generator, Action<T> body)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(body);
        return new Property<T>(generator, AsPredicate(body));
    }

    /// <summary>
    /// Creates a property that holds when <paramref name="body"/> returns <see langword="true"/> without
    /// throwing for every value produced by <paramref name="generator"/>.
    /// </summary>
    /// <typeparam name="T">The type of value the property is checked over.</typeparam>
    /// <param name="generator">The generator that produces the examples.</param>
    /// <param name="body">The predicate to evaluate for each example.</param>
    /// <returns>
    /// A property that can be checked with <see cref="Property{T}.Assert"/> or
    /// <see cref="Property{T}.Check"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator"/> or <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    public static Property<T> ForAll<T>(Generator<T> generator, Func<T, bool> body)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(body);
        return new Property<T>(generator, body);
    }

    /// <summary>
    /// Creates a property that holds when <paramref name="body"/> returns without throwing for every
    /// pair of values drawn independently from the two generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each pair.</param>
    /// <param name="generator2">The generator that produces the second value of each pair.</param>
    /// <param name="body">The action to run on each pair.</param>
    /// <returns>
    /// A property that can be checked with <see cref="Property{T}.Assert"/> or
    /// <see cref="Property{T}.Check"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/> or <paramref name="body"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Property<(T1, T2)> ForAll<T1, T2>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Action<T1, T2> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(Generate.Tuple(generator1, generator2), pair => body(pair.Item1, pair.Item2));
    }

    /// <summary>
    /// Creates a property that holds when <paramref name="body"/> returns <see langword="true"/> without
    /// throwing for every pair of values drawn independently from the two generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each pair.</param>
    /// <param name="generator2">The generator that produces the second value of each pair.</param>
    /// <param name="body">The predicate to evaluate for each pair.</param>
    /// <returns>
    /// A property that can be checked with <see cref="Property{T}.Assert"/> or
    /// <see cref="Property{T}.Check"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/> or <paramref name="body"/> is
    /// <see langword="null"/>.
    /// </exception>
    public static Property<(T1, T2)> ForAll<T1, T2>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Func<T1, T2, bool> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(Generate.Tuple(generator1, generator2), pair => body(pair.Item1, pair.Item2));
    }

    /// <summary>
    /// Creates a property that holds when <paramref name="body"/> returns without throwing for every
    /// triple of values drawn independently from the three generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each triple.</param>
    /// <param name="generator2">The generator that produces the second value of each triple.</param>
    /// <param name="generator3">The generator that produces the third value of each triple.</param>
    /// <param name="body">The action to run on each triple.</param>
    /// <returns>
    /// A property that can be checked with <see cref="Property{T}.Assert"/> or
    /// <see cref="Property{T}.Check"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/> or
    /// <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    public static Property<(T1, T2, T3)> ForAll<T1, T2, T3>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Action<T1, T2, T3> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(
            Generate.Tuple(generator1, generator2, generator3),
            triple => body(triple.Item1, triple.Item2, triple.Item3));
    }

    /// <summary>
    /// Creates a property that holds when <paramref name="body"/> returns <see langword="true"/> without
    /// throwing for every triple of values drawn independently from the three generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each triple.</param>
    /// <param name="generator2">The generator that produces the second value of each triple.</param>
    /// <param name="generator3">The generator that produces the third value of each triple.</param>
    /// <param name="body">The predicate to evaluate for each triple.</param>
    /// <returns>
    /// A property that can be checked with <see cref="Property{T}.Assert"/> or
    /// <see cref="Property{T}.Check"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/> or
    /// <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    public static Property<(T1, T2, T3)> ForAll<T1, T2, T3>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Func<T1, T2, T3, bool> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(
            Generate.Tuple(generator1, generator2, generator3),
            triple => body(triple.Item1, triple.Item2, triple.Item3));
    }

    // An `async x => { ... }` body also converts to the Action<T> overload above, as `async void`:
    // the check loop cannot await such a body, so the property would pass whatever it does.
    // Overload resolution already prefers the Task-returning overloads for it; the priority on each
    // states that preference outright so it cannot drift as overloads are added.

    /// <summary>
    /// Creates a property that holds when the asynchronous <paramref name="body"/> completes
    /// without throwing for every value produced by <paramref name="generator"/>.
    /// </summary>
    /// <typeparam name="T">The type of value the property is checked over.</typeparam>
    /// <param name="generator">The generator that produces the examples.</param>
    /// <param name="body">The asynchronous action to run on each example.</param>
    /// <returns>
    /// A property that can be checked with <see cref="AsyncProperty{T}.AssertAsync"/> or
    /// <see cref="AsyncProperty{T}.CheckAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator"/> or <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    [OverloadResolutionPriority(1)]
    public static AsyncProperty<T> ForAll<T>(Generator<T> generator, Func<T, Task> body)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(body);
        return new AsyncProperty<T>(generator, AsPredicate(body));
    }

    /// <summary>
    /// Creates a property that holds when the asynchronous <paramref name="body"/> returns
    /// <see langword="true"/> without throwing for every value produced by
    /// <paramref name="generator"/>.
    /// </summary>
    /// <typeparam name="T">The type of value the property is checked over.</typeparam>
    /// <param name="generator">The generator that produces the examples.</param>
    /// <param name="body">The asynchronous predicate to evaluate for each example.</param>
    /// <returns>
    /// A property that can be checked with <see cref="AsyncProperty{T}.AssertAsync"/> or
    /// <see cref="AsyncProperty{T}.CheckAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator"/> or <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    [OverloadResolutionPriority(1)]
    public static AsyncProperty<T> ForAll<T>(Generator<T> generator, Func<T, Task<bool>> body)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(body);
        return new AsyncProperty<T>(generator, value => new ValueTask<bool>(body(value)));
    }

    /// <summary>
    /// Creates a property that holds when the asynchronous <paramref name="body"/> completes
    /// without throwing for every pair of values drawn independently from the two generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each pair.</param>
    /// <param name="generator2">The generator that produces the second value of each pair.</param>
    /// <param name="body">The asynchronous action to run on each pair.</param>
    /// <returns>
    /// A property that can be checked with <see cref="AsyncProperty{T}.AssertAsync"/> or
    /// <see cref="AsyncProperty{T}.CheckAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/> or <paramref name="body"/> is
    /// <see langword="null"/>.
    /// </exception>
    [OverloadResolutionPriority(1)]
    public static AsyncProperty<(T1, T2)> ForAll<T1, T2>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Func<T1, T2, Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(Generate.Tuple(generator1, generator2), pair => body(pair.Item1, pair.Item2));
    }

    /// <summary>
    /// Creates a property that holds when the asynchronous <paramref name="body"/> returns
    /// <see langword="true"/> without throwing for every pair of values drawn independently from the
    /// two generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each pair.</param>
    /// <param name="generator2">The generator that produces the second value of each pair.</param>
    /// <param name="body">The asynchronous predicate to evaluate for each pair.</param>
    /// <returns>
    /// A property that can be checked with <see cref="AsyncProperty{T}.AssertAsync"/> or
    /// <see cref="AsyncProperty{T}.CheckAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/> or <paramref name="body"/> is
    /// <see langword="null"/>.
    /// </exception>
    [OverloadResolutionPriority(1)]
    public static AsyncProperty<(T1, T2)> ForAll<T1, T2>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Func<T1, T2, Task<bool>> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(Generate.Tuple(generator1, generator2), pair => body(pair.Item1, pair.Item2));
    }

    /// <summary>
    /// Creates a property that holds when the asynchronous <paramref name="body"/> completes
    /// without throwing for every triple of values drawn independently from the three generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each triple.</param>
    /// <param name="generator2">The generator that produces the second value of each triple.</param>
    /// <param name="generator3">The generator that produces the third value of each triple.</param>
    /// <param name="body">The asynchronous action to run on each triple.</param>
    /// <returns>
    /// A property that can be checked with <see cref="AsyncProperty{T}.AssertAsync"/> or
    /// <see cref="AsyncProperty{T}.CheckAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/> or
    /// <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    [OverloadResolutionPriority(1)]
    public static AsyncProperty<(T1, T2, T3)> ForAll<T1, T2, T3>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Func<T1, T2, T3, Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(
            Generate.Tuple(generator1, generator2, generator3),
            triple => body(triple.Item1, triple.Item2, triple.Item3));
    }

    /// <summary>
    /// Creates a property that holds when the asynchronous <paramref name="body"/> returns
    /// <see langword="true"/> without throwing for every triple of values drawn independently from
    /// the three generators.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="generator1">The generator that produces the first value of each triple.</param>
    /// <param name="generator2">The generator that produces the second value of each triple.</param>
    /// <param name="generator3">The generator that produces the third value of each triple.</param>
    /// <param name="body">The asynchronous predicate to evaluate for each triple.</param>
    /// <returns>
    /// A property that can be checked with <see cref="AsyncProperty{T}.AssertAsync"/> or
    /// <see cref="AsyncProperty{T}.CheckAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="generator1"/>, <paramref name="generator2"/>, <paramref name="generator3"/> or
    /// <paramref name="body"/> is <see langword="null"/>.
    /// </exception>
    [OverloadResolutionPriority(1)]
    public static AsyncProperty<(T1, T2, T3)> ForAll<T1, T2, T3>(
        Generator<T1> generator1,
        Generator<T2> generator2,
        Generator<T3> generator3,
        Func<T1, T2, T3, Task<bool>> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return ForAll(
            Generate.Tuple(generator1, generator2, generator3),
            triple => body(triple.Item1, triple.Item2, triple.Item3));
    }

    private static Func<T, bool> AsPredicate<T>(Action<T> body) => value =>
    {
        body(value);
        return true;
    };

    private static Func<T, ValueTask<bool>> AsPredicate<T>(Func<T, Task> body) => async value =>
    {
        await body(value).ConfigureAwait(false);
        return true;
    };
}
