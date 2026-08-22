using System.Runtime.CompilerServices;
using QuickCheck.Running;

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
    /// The sink for the example the runner is currently executing the body on, or
    /// <see langword="null"/> outside a body. The runner sets it around the body call, and the
    /// execution context carries it into awaited continuations and parallel work the body starts.
    /// </summary>
    internal static readonly AsyncLocal<ExampleStatistics?> CurrentStatistics = new();

    /// <summary>
    /// Counts the current example under <paramref name="label"/> when <paramref name="condition"/>
    /// is <see langword="true"/>, so that the report shows the percentage of examples that hit it.
    /// </summary>
    /// <param name="condition">Whether the current example belongs under the label.</param>
    /// <param name="label">The label, printed as given.</param>
    /// <exception cref="ArgumentException"><paramref name="label"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// The call was not made from a property body: from generator code, say, or from a thread that
    /// does not flow the body's execution context.
    /// </exception>
    /// <remarks>
    /// A label counts at most once per example, however many times the body reports it, and only
    /// examples that pass count: discarded examples and the candidates evaluated while shrinking do
    /// not. A label whose condition is <see langword="false"/> on every example still appears in the
    /// report, at 0%, which is the case the report exists to show. Percentages are of
    /// <see cref="PropertyResult{T}.TestsRun"/>.
    /// </remarks>
    public static void Classify(bool condition, string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        Current(nameof(Classify)).Label(label, condition);
    }

    /// <summary>
    /// Counts the current example under <paramref name="label"/>; the same as
    /// <see cref="Classify"/> with a <see langword="true"/> condition.
    /// </summary>
    /// <param name="label">The label, printed as given.</param>
    /// <exception cref="ArgumentException"><paramref name="label"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidOperationException">
    /// The call was not made from a property body: from generator code, say, or from a thread that
    /// does not flow the body's execution context.
    /// </exception>
    /// <remarks>
    /// A label counts at most once per example, and only examples that pass count: discarded
    /// examples and the candidates evaluated while shrinking do not.
    /// </remarks>
    public static void Label(string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        Current(nameof(Label)).Label(label, hit: true);
    }

    /// <summary>
    /// Counts the current example under <paramref name="value"/> in the table called
    /// <paramref name="name"/>, so that the report shows how the examples were distributed over
    /// the values the body saw.
    /// </summary>
    /// <param name="name">The table to count the value in, printed as the table's heading.</param>
    /// <param name="value">
    /// The value to count the example under. It is used verbatim as the key in
    /// <see cref="PropertyStatistics.Tables"/> and printed as given, so format it as you want to
    /// read it.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> or <paramref name="value"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The call was not made from a property body: from generator code, say, or from a thread that
    /// does not flow the body's execution context.
    /// </exception>
    /// <remarks>
    /// A value counts at most once per example in each table, so a body that collects each command
    /// of a command list counts every distinct command the example ran. Only examples that pass
    /// count: discarded examples and the candidates evaluated while shrinking do not. Percentages
    /// are of <see cref="PropertyResult{T}.TestsRun"/>.
    /// </remarks>
    public static void Collect(string name, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(value);
        Current(nameof(Collect)).Collect(name, value);
    }

    /// <summary>
    /// Counts the current example under <paramref name="label"/> when <paramref name="condition"/>
    /// is <see langword="true"/>, as <see cref="Classify"/> does, and requires at least
    /// <paramref name="minimumPercent"/> of the passed examples to hit the label, failing the check
    /// with <see cref="PropertyOutcome.InsufficientCoverage"/> if they do not.
    /// </summary>
    /// <param name="condition">Whether the current example hits the label.</param>
    /// <param name="minimumPercent">
    /// The percentage of passed examples, from 0 to 100, that must hit the label; 100 requires
    /// every example to.
    /// </param>
    /// <param name="label">The label, printed as given.</param>
    /// <exception cref="ArgumentException"><paramref name="label"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="minimumPercent"/> is <see cref="double.NaN"/> or outside 0 to 100.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The call was not made from a property body: from generator code, say, or from a thread that
    /// does not flow the body's execution context.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Call it on every example, with the condition as the argument, rather than from inside a
    /// branch: the percentage is of every passed example, not of the examples that reach the call,
    /// so a branch-guarded call under-counts and fails on a requirement the caller only meant to
    /// hold within the branch. A call no example reaches states no requirement at all. Two calls
    /// for one label take the larger minimum.
    /// </para>
    /// <para>
    /// The requirement is a floor that catches a distribution far off what you intended, not an
    /// assertion about the rate the generator really produces: it is a plain threshold over the
    /// <see cref="CheckOptions.RunCount"/> examples of one seed, so a minimum near the rate you
    /// actually expect fails on an unlucky seed. State a minimum you would want to be told about
    /// falling below, and read the real rate off the report. It is not checked when a single
    /// example is replayed through
    /// <see cref="CheckOptions.Replay"/>, and a falsified or exhausted check reports that outcome
    /// instead.
    /// </para>
    /// </remarks>
    public static void Cover(bool condition, double minimumPercent, string label)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);

        if (double.IsNaN(minimumPercent) || minimumPercent < 0 || minimumPercent > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumPercent), minimumPercent, "The minimum percentage must be between 0 and 100.");
        }

        Current(nameof(Cover)).Cover(label, condition, minimumPercent);
    }

    private static ExampleStatistics Current(string method) =>
        CurrentStatistics.Value
        ?? throw new InvalidOperationException($"Property.{method} was called outside a property body.");

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
