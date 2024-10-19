using System.Diagnostics;
using QuickCheck.Exceptions;
using QuickCheck.Generators;

namespace QuickCheck;

/// <summary>
/// <para>
/// The QuickChecker class used for running property-based tests against a
/// target method.
/// </para>
/// <para>
/// To create an instance, use the <see cref="CreateDefault()"/> or
/// <see cref="CreateEmpty()"/> methods.
/// </para>
/// </summary>
public sealed class QuickChecker
{
    public const int DefaultRunCount = 10_000;

    private readonly Dictionary<Type, object> _generators = new();

    private readonly int _runCount;

    private QuickChecker()
    {
        _runCount = DefaultRunCount;
    }

    private QuickChecker(int runCount)
    {
        _runCount = runCount;
    }

    /// <summary>
    /// Creates a new instance of <see cref="QuickChecker"/> using the default
    /// <see cref="QuickCheckerOptions"/>.
    /// </summary>
    public static QuickChecker CreateDefault()
    {
        return CreateDefault(QuickCheckerOptions.Default);
    }

    /// <summary>
    /// Creates a new instance of <see cref="QuickChecker"/> with the default
    /// generators registered and the specified <paramref name="configure"/>
    /// action applied.
    /// </summary>
    public static QuickChecker CreateDefault(
        Action<QuickCheckerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new QuickCheckerOptions();
        configure(options);
        return CreateDefault(options);
    }

    /// <summary>
    /// Creates a new instance of <see cref="QuickChecker"/> configured
    /// with the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The configuration of the <see cref="QuickChecker"/>.
    /// </param>
    public static QuickChecker CreateDefault(QuickCheckerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var checker = CreateEmpty(options);

        if (options.Random is null || options.Random == Random.Shared)
        {
            checker.AddGenerator(ArbitraryBooleanGenerator.Default);
            checker.AddGenerator(ArbitraryInt16Generator.Default);
            checker.AddGenerator(ArbitraryInt32Generator.Default);
            checker.AddGenerator(ArbitraryInt64Generator.Default);
            checker.AddGenerator(ArbitraryUInt16Generator.Default);
            checker.AddGenerator(ArbitraryUInt32Generator.Default);
            checker.AddGenerator(ArbitraryUInt64Generator.Default);
            checker.AddGenerator(ArbitraryCharGenerator.Default);

            if (options.MaximumSize is { } maximumSize)
            {
                checker.AddGenerator(
                    new ArbitraryStringGenerator(Random.Shared, maximumSize));
            }
            else
            {
                checker.AddGenerator(ArbitraryStringGenerator.Default);
            }
        }
        else
        {
            checker.AddGenerator(new ArbitraryBooleanGenerator(options.Random));
            checker.AddGenerator(new ArbitraryInt16Generator(options.Random));
            checker.AddGenerator(new ArbitraryInt32Generator(options.Random));
            checker.AddGenerator(new ArbitraryInt64Generator(options.Random));
            checker.AddGenerator(new ArbitraryUInt16Generator(options.Random));
            checker.AddGenerator(new ArbitraryUInt32Generator(options.Random));
            checker.AddGenerator(new ArbitraryUInt64Generator(options.Random));
            checker.AddGenerator(new ArbitraryCharGenerator(options.Random));

            if (options.MaximumSize is { } maximumSize)
            {
                checker.AddGenerator(
                    new ArbitraryStringGenerator(options.Random, maximumSize));
            }
            else
            {
                checker.AddGenerator(
                    new ArbitraryStringGenerator(options.Random));
            }
        }

        return checker;
    }

    /// <summary>
    /// Creates a new instance of <see cref="QuickChecker"/> with no generators
    /// registered.
    /// </summary>
    public static QuickChecker CreateEmpty() =>
        CreateEmpty(QuickCheckerOptions.Default);

    /// <summary>
    /// Creates a new instance of <see cref="QuickChecker"/> with no generators
    /// registered and the specified <paramref name="configure"/> configuration
    /// applied.
    /// </summary>
    /// <param name="configure">
    /// The configuration of the <see cref="QuickChecker"/>.
    /// </param>
    public static QuickChecker CreateEmpty(
        Action<QuickCheckerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new QuickCheckerOptions();
        configure(options);
        return CreateEmpty(options);
    }

    /// <summary>
    /// Creates a new instance of <see cref="QuickChecker"/> with no generators
    /// registered and the specified <paramref name="options"/>.
    /// </summary>
    /// <param name="options">
    /// The configuration of the <see cref="QuickChecker"/>.
    /// </param>
    public static QuickChecker CreateEmpty(QuickCheckerOptions options)
    {
        return new QuickChecker(options.RunCount);
    }

    /// <summary>
    /// Adds the specified <paramref name="generator"/> for the type
    /// <typeparamref name="T"/> to this <see cref="QuickChecker"/> instance.
    /// </summary>
    /// <param name="generator">The generator to add.</param>
    /// <typeparam name="T">
    /// The type that the <paramref name="generator"/> generates.
    /// </typeparam>
    /// <exception cref="InvalidOperationException">
    /// A generator for type <typeparamref name="T"/> is already registered.
    /// </exception>
    public void AddGenerator<T>(IArbitraryValueGenerator<T> generator)
    {
        ArgumentNullException.ThrowIfNull(generator);

        if (!TryAddGenerator(generator))
        {
            throw new InvalidOperationException(
                $"A generator for type '{typeof(T).FullName}' is already registered");
        }
    }

    /// <summary>
    /// Attempts to add the specified <paramref name="generator"/> for the type
    /// <typeparamref name="T"/> to this <see cref="QuickChecker"/> instance.
    /// </summary>
    /// <param name="generator">The generator to add.</param>
    /// <typeparam name="T">
    /// The type that the <paramref name="generator"/> generates.
    /// </typeparam>
    /// <returns>
    /// <see langword="true"/> if the <paramref name="generator"/> was
    /// successfully added, otherwise <see langword="false"/>
    /// </returns>
    public bool TryAddGenerator<T>(IArbitraryValueGenerator<T> generator)
    {
        return _generators.TryAdd(typeof(T), generator);
    }

    // T1
    public TestResult<TInput> Run<TInput, TOutput>(
        Func<TInput, TOutput> target,
        Func<TOutput, bool>? validate = null)
    {
        var type = typeof(TInput);

        if (!_generators.TryGetValue(type, out var value))
        {
            MissingGeneratorException.Throw(typeof(TInput));
        }

        if (value is not IArbitraryValueGenerator<TInput> generator)
        {
            throw new UnreachableException(
                $"A non-generator was returned from {nameof(_generators)}.");
        }

        for (var i = 0; i < _runCount; i++)
        {
            var input = generator.Generate();

            if (RunCore(target, input, validate).IsError)
            {
                return ShrinkFailure(input, target, validate, generator) switch
                {
                    { } testResult => testResult,
                    _ => TestResult.CreateFailed(input)
                };
            }
        }

        return TestResult.CreateSuccess<TInput>(default!);
    }

    // T1, T2
    public TestResult<(T1, T2)> Run<T1, T2, TOutput>(
        Func<T1, T2, TOutput> target,
        Func<TOutput, bool>? validate = null)
    {
        var g1 = (IArbitraryValueGenerator<T1>)_generators[typeof(T1)];
        var g2 = (IArbitraryValueGenerator<T2>)_generators[typeof(T2)];

        var generator =
            new ArbitraryTupleGenerator<T1, T2>(g1, g2, Random.Shared);
        _generators.Add(typeof((T1, T2)), generator);

        return Run<(T1, T2), TOutput>(Func, validate);
        TOutput Func((T1, T2) tuple) => target(tuple.Item1, tuple.Item2);
    }

    // T1, T2, T3
    public TestResult<(T1, T2, T3)> Run<T1, T2, T3, TOutput>(
        Func<T1, T2, T3, TOutput> target,
        Func<TOutput, bool>? validate = null)
    {
        var g1 = (IArbitraryValueGenerator<T1>)_generators[typeof(T1)];
        var g2 = (IArbitraryValueGenerator<T2>)_generators[typeof(T2)];
        var g3 = (IArbitraryValueGenerator<T3>)_generators[typeof(T3)];

        var generator =
            new ArbitraryTupleGenerator<T1, T2, T3>(g1, g2, g3, Random.Shared);
        _generators.Add(typeof((T1, T2, T3)), generator);

        return Run<(T1, T2, T3), TOutput>(Func, validate);

        TOutput Func((T1, T2, T3) tuple) =>
            target(tuple.Item1, tuple.Item2, tuple.Item3);
    }

    private static TestResult<TInput> RunCore<TInput, TOutput>(
        Func<TInput, TOutput> target,
        TInput input,
        Func<TOutput, bool>? validate)
    {
        TOutput result;

        try
        {
            result = target(input);
        }
        catch (Exception exception)
        {
            return TestResult.CreateFailed(input, exception);
        }

        // Calling `target` did not throw, and there is no validation function
        // to run.
        if (validate is null)
        {
            return TestResult.CreateSuccess(input);
        }

        try
        {
            var isValid = validate(result);

            if (!isValid)
            {
                return TestResult.CreateFailed(input);
            }
        }
        catch (Exception ex)
        {
            // The user's validation function threw an exception. Bail.
            QuickCheckRunException.ThrowValidationFunctionThrew(
                input?.ToString() ?? "null", ex);
        }

        return TestResult.CreateSuccess(input);
    }

    private static TestResult<TInput>? ShrinkFailure<TInput, TOutput>(
        TInput input,
        Func<TInput, TOutput> target,
        Func<TOutput, bool>? validate,
        IArbitraryValueGenerator<TInput> generator)
    {
        foreach (var shrunkInput in generator.Shrink(input))
        {
            var result = RunCore(target, shrunkInput, validate);

            if (!result.IsError)
            {
                // Didn't reproduce on `shrunkInput` but continue trying any smaller values.
                continue;
            }

            // `shrunk` causes an error too. Recursively shrink until we get `null`. When we do, `result` is the
            // smallest input we've reproduced with.
            return ShrinkFailure(result.Input, target, validate, generator) ?? result;
        }

        return null;
    }
}
