using System.Diagnostics;
using QuickCheck.Generators;

namespace QuickCheck;

public sealed class QuickChecker
{
    public const int DefaultRunCount = 10_000;

    private static readonly Dictionary<Type, object> BuiltInGenerators = new()
    {
        { typeof(short), ArbitraryInt16Generator.Default },
        { typeof(int), ArbitraryInt32Generator.Default },
        { typeof(long), ArbitraryInt32Generator.Default },
        { typeof(ushort), ArbitraryUInt16Generator.Default },
        { typeof(uint), ArbitraryUInt32Generator.Default },
        { typeof(ulong), ArbitraryUInt32Generator.Default },
        { typeof(char), ArbitraryCharGenerator.Default },
        { typeof(string), ArbitraryStringGenerator.Default },
    };

    private readonly Dictionary<Type, object> _generators = new(BuiltInGenerators);

    public int RunCount { get; }

    public QuickChecker()
    {
        RunCount = DefaultRunCount;
    }

    public void AddGenerator<T>(IArbitraryValueGenerator<T> generator)
    {
        if (!TryAddGenerator(generator))
        {
            throw new ArgumentException($"A generator for type '{typeof(T).FullName}' is already registered",
                nameof(generator));
        }
    }

    public bool TryAddGenerator<T>(IArbitraryValueGenerator<T> generator)
    {
        return _generators.TryAdd(typeof(T), generator);
    }

    // T1
    public TestResult<TInput> Run<TInput, TOutput>(Func<TInput, TOutput> target, Func<TOutput, bool> validate)
    {
        var type = typeof(TInput);

        if (!_generators.TryGetValue(type, out var value))
        {
            MissingGeneratorException.Throw(typeof(TInput));
        }

        if (value is not IArbitraryValueGenerator<TInput> generator)
        {
            throw new UnreachableException($"A non-generator was returned from {nameof(_generators)}.");
        }

        for (var i = 0; i < RunCount; i++)
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

    // // T1, T2
    // public TestResult<(T1, T2)> Run<T1, T2, TOutput>(Func<T1, T2, TOutput> target, Func<TOutput, bool> validate)
    //     where T1 : IComparable<T1>
    //     where T2 : IComparable<T2>
    // {
    //     ReadOnlySpan<Type> argTypes = [typeof(T1), typeof(T2)];
    //     return Run(argTypes, target, validate);
    // }


    private static TestResult<TInput> RunCore<TInput, TOutput>(
        Func<TInput, TOutput> target,
        TInput input,
        Func<TOutput, bool> validate)
    {
        try
        {
            var result = target(input);

            try
            {
                var isValid = validate(result);

                if (!isValid)
                {
                    return TestResult.CreateFailed(input);
                }
            }
            catch
            {
                Console.Error.WriteLine(
                    $"ERROR: The provided validation function threw on the output target method with input '{input}'.");
                throw;
            }
        }
        catch (Exception)
        {
            return TestResult.CreateFailed(input);
        }

        return TestResult.CreateSuccess(input);
    }

    private static TestResult<TInput>? ShrinkFailure<TInput, TOutput>(
        TInput input,
        Func<TInput, TOutput> target,
        Func<TOutput, bool> validate,
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