# QuickCheck for .NET

[![NuGet](https://img.shields.io/nuget/v/QuickCheck?label=QuickCheck)](https://www.nuget.org/packages/QuickCheck)
[![NuGet](https://img.shields.io/nuget/v/QuickCheck.Xunit.v3?label=QuickCheck.Xunit.v3)](https://www.nuget.org/packages/QuickCheck.Xunit.v3)
[![Build and Test](https://github.com/rabuckley/quickcheck-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/rabuckley/quickcheck-dotnet/actions/workflows/ci.yml)

Property-based testing for .NET.

| Package               | What it is                                                                               | Docs                                        |
| --------------------- | ---------------------------------------------------------------------------------------- | ------------------------------------------- |
| `QuickCheck`          | The library: generators, properties, shrinking, reporting. No test-framework dependency. | [readme](src/QuickCheck/readme.md)          |
| `QuickCheck.Xunit.v3` | A `[Property]` attribute for xUnit v3 (4.0+) that generates a test method's parameters.  | [readme](src/QuickCheck.Xunit.v3/readme.md) |

- [Quick start](#quick-start)
- [Why property-based testing](#why-property-based-testing)
- [Why this library](#why-this-library)
- [Resources](#resources)

## Quick start

In an xUnit v3 test project:

```shell
dotnet add package QuickCheck.Xunit.v3
```

```csharp
using QuickCheck;
using QuickCheck.Xunit;

public sealed class EncodingTests
{
    [Property]
    public void Roundtrips(string s) => Assert.Equal(s, Decode(Encode(s)));
}
```

With any other test framework:

```shell
dotnet add package QuickCheck
```

```csharp
using QuickCheck;

[Fact]
public void Reversing_twice_is_the_identity()
{
    Property
        .ForAll(Generate.Integer<int>().List(), list =>
            list.AsEnumerable().Reverse().Reverse().SequenceEqual(list))
        .Assert();
}
```

Either way, a failure reports the smallest input that still fails and how to replay that input again:

```
Falsified after 12 tests and 34 shrinks (seed 3468194371).
  Minimal counterexample: [100]
  Original counterexample: [-3, 77, 100, 5, -19]
  Replay with: new CheckOptions { Replay = Replay.Parse("3468194371:11") }
```

For more information read the [QuickCheck readme](src/QuickCheck/readme.md) which covers generators, properties, options, replay, and statistics, and the [QuickCheck.Xunit.v3 readme](src/QuickCheck.Xunit.v3/readme.md) which covers `[Property]`, where parameter generators come from, and the attribute's settings.

## Why property-based testing

A standard unit test defines an input, runs the tested code and checks the output against a value you worked out by hand:

```csharp
[Fact]
public void Reverse_reverses()
{
    Assert.Equal([3, 2, 1], Reverse([1, 2, 3]));
}
```

That proves the code works for `[1, 2, 3]` but not an empty list, a single element, lists with duplicates, `int.MinValue`, or a list of ten thousand items, unless you thought of and wrote those cases too. The cases cases you _didn't_ think to test are often where the bugs are.

Instead of defining a fixed input, property-based tests describe the _kind_ of input the code should handle, and instead of stating one expected output, you state something that must be true of the output for every input: a _property_. This library then generates hundreds of inputs, checks the property holds against each and reports the first one that breaks it.

Because the inputs are randomised, when a property fails the input that broke it is usually large and mised with irrelevant detail. We therefore shrink failures by searching for the "smallest" input that still fails.

### Finding properties

The hard part of property-based testing is identifying what the properties of your system are. Some common patterns are:

- **Round trips.** Encode then decode, serialise then deserialise, save then load. In each case you should get back what you started with (`Decode(Encode(s)) == s`.)
- **Invariants.** Whatever the input, the output has some shape. For example, a sort returns a list of the same length, a balance never goes negative, a Raft implementation never elects two leaders in a single term.
- **Idempotence.** Doing it twice is the same as doing it once (`Trim(Trim(s)) == Trim(s)`.)
- **Commutativity and other algebra.** `Add(a, b) == Add(b, a)`; merging two configs then a third gives the same result as merging the second and third first.
- **A reference implementation.** Your optimised algorithm should agree with the slow, obviously-correct one on every input. The "oracle" can be a naive loop, an old implementation you're replacing, or a call to a well-tested library.
- **It doesn't throw.** For all inputs of this shape, the code completes without throwing an exception. Parsers, validators and anything handling untrusted input benefit from this.

## Why this library

This library has made a few important choices:

- **Shrinking is free for every generator.** Generation is recorded as a sequence of integer choices and shrinking edits that sequence, the approach [Hypothesis](https://hypothesis.readthedocs.io/) takes. A generator you write with `Select`, `Where`, `SelectMany`, or `Generate.From` shrinks as well as a built-in one, and there is no shrinker to write by hand.
- **Generators are ordinary values.** `Generator<T>` composes with LINQ, including query syntax for dependent generation, and a type can carry its own default generator through `IArbitrary<T>`.
- **Failures replay anywhere.** The library owns its random number generator, so a seed produces the same examples on every machine and runtime, and every report prints the token that reruns its counterexample.
- **Modern .NET.** Asynchronous property bodies, cancellation, numeric generics, nullable annotations that add `null` examples and an AOT-compatible core library with no dependencies.
- **An xUnit.net v3 adapter.** `[Property]` derives generators for parameters, records included and reports the shrunk counterexample and replay token in the test output.

## Resources

- [Haskell's QuickCheck](https://hackage.haskell.org/package/QuickCheck), the original property-based testing library, and the paper that introduced it: [_QuickCheck: A Lightweight Tool for Random Testing of Haskell Programs_](https://www.cs.tufts.edu/~nr/cs257/archive/john-hughes/quick.pdf)
- [Hypothesis](https://hypothesis.readthedocs.io/), whose choice-sequence approach to shrinking this library follows

## License

[MIT](LICENSE).
