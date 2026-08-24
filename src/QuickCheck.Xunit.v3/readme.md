# QuickCheck.Xunit.v3

The xUnit v3 adapter for [QuickCheck](https://www.nuget.org/packages/QuickCheck), a property-based testing library for .NET. This library contains a `[Property]` attribute that generates each parameter of a test method, runs many examples and reports a shrunk counterexample when it fails. Everything else about the test (fixtures, `ITestOutputHelper`, `Skip`, `Timeout`, traits) works as for `[Fact]`.

Requires xunit.v3 4.0.0 or later.

- [Install](#install)
- [Writing properties](#writing-properties)
- [Where generators come from](#where-generators-come-from)
- [Settings](#settings)
- [Reports](#reports)

## Install

```shell
dotnet add package QuickCheck.Xunit.v3
```

The package references the core `QuickCheck` library, so `Generate`, `Property.Assume` and the statistics statics are all available.

## Writing properties

```csharp
using QuickCheck;
using QuickCheck.Xunit;

public sealed class EncodingTests
{
    [Property]
    public void Round_trips(string s) => Assert.Equal(s, Decode(Encode(s)));

    [Property(RunCount = 500)]
    public bool Sorting_is_idempotent(List<int> items) =>
        items.Order().SequenceEqual(items.Order().Order());

    [Property]
    public async Task Handles_any_request(Request request) => await Handle(request);
}
```

A method may return `void`, `bool`, `Task`, `ValueTask`, `Task<bool>`, or `ValueTask<bool>`. A `bool` result of `false` fails the example and anything else fails by throwing, so ordinary xUnit assertions work. The body may call `Property.Assume` to discard an example and `Property.Classify`, `Label`, `Collect`, and `Cover` to report what it exercised.

You can also use `Property.ForAll` inside an ordinary `[Fact]` test.

## Where generators come from

A parameter's generator is found, in order, from:

1. `[Generator(nameof(Member))]` on the parameter: a static `Generator<T>` property, field, or parameterless method on the test class (or on the attribute's `Generators` type, or on an explicit `[Generator(typeof(Source), "Member")]`). It applies to a record's positional parameters too, so a nested member can name its own generator.
2. A **public** static `Generator<T>` member of the attribute's `Generators` type, matched by type. This also applies to nested members of records.
3. The type's `IArbitrary<T>` implementation.
4. Built-ins: integers, `double`, `float`, `Half`, `NFloat`, `decimal`, `bool`, `char`, `string`, enums, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `Guid`, `Nullable<T>`, arrays, `List<T>`, `HashSet<T>` and `Dictionary<TKey, TValue>` with their interfaces, tuples, and any type with a single public constructor (records included), derived recursively. Nullable annotations add `null` examples, though never as a dictionary key.

A `double`, `float`, `Half` or `NFloat` parameter draws from the full range, so it gets `NaN`, both infinities and `-0.0` as well as finite values. For finite values only, or any narrower range, name a generator built from `Generate.FloatingPoint(min, max)` with `[Generator]`.

```csharp
public sealed class AccountTests
{
    private static Generator<int> Small { get; } = Generate.Between(-10, 10);

    [Property]
    public void Deposits_commute([Generator(nameof(Small))] int a, [Generator(nameof(Small))] int b) =>
        Assert.Equal(Deposit(Deposit(Empty, a), b), Deposit(Deposit(Empty, b), a));

    // Every int in this method, including the one inside Transfer, draws from Generators.Small.
    [Property(Generators = typeof(Generators))]
    public void Transfers_preserve_total(Transfer transfer) =>
        Assert.Equal(Total(Apply(transfer)), Total(Empty));

    public static class Generators
    {
        public static Generator<int> Small => Generate.Between(-10, 10);
    }
}
```

## Settings

`[Property]` accepts the same options as `CheckOptions` in the core library, plus `Generators`:

| Setting             | Default   | Effect                                                                                             |
| ------------------- | --------- | -------------------------------------------------------------------------------------------------- |
| `RunCount`          | 100       | Examples to try before passing.                                                                    |
| `Seed`              | random    | Fixes the example sequence.                                                                        |
| `Replay`            | none      | A token from a failure report, for example `"3468194371:11"`; runs only that example.              |
| `MaxShrinkAttempts` | 10,000    | Candidates the shrinker may try; 0 disables shrinking.                                             |
| `MaxShrinkWork`     | 5,000,000 | Total choices shrinking may replay.                                                                |
| `CheckCoverage`     | false     | Fails the test when a `Property.Cover` requirement is known to be missed; see [Reports](#reports). |
| `Generators`        | none      | A type whose public static `Generator<T>` members supply generators by type.                       |

## Reports

A failure reads:

```
Falsified after 12 tests and 34 shrinks (seed 3468194371).
  Minimal counterexample: s = "\u0000"
  Original counterexample: s = "K\u0000ap9"
  Replay with: [Property(Replay = "3468194371:11")]
```

A passing property writes `Passed 100 tests (seed …)` and any label distribution to the test output. An unmet `Property.Cover` requirement prints there as `Only 3% label, but required 20%` and the test still passes; with `CheckCoverage = true` the property runs past `RunCount` until the requirement is known to be met or missed, and a known miss fails the test with `Insufficient coverage after … tests`. Deciding a small minimum can take a million or more examples, so give such a test a `Timeout`.
