# QuickCheck for .NET

Property-based testing for C#.

A conventional unit test picks an input, runs the code, and checks the output against a value you worked out by hand:

```csharp
[Fact]
public void Reverse_reverses()
{
    Assert.Equal(new[] { 3, 2, 1 }, Reverse(new[] { 1, 2, 3 }));
}
```

That proves the code works for `[1, 2, 3]`. It says nothing about the empty list, a single element, duplicates, `int.MinValue`, or a list of ten thousand items — unless you thought to write those cases too, and the cases you *didn't* think of are exactly where the bugs are.

A property-based test turns this round. Instead of choosing the input, you describe the *kind* of input the code should handle, and instead of stating one expected output, you state something that must be true of the output for **every** input — a *property*. The library then generates hundreds of inputs, runs the property against each, and reports the first one that breaks it:

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

Read that as: *for all lists of integers, reversing twice gives back the original.* Each run tries 100 freshly generated lists — empty ones, long ones, ones full of extreme values — and every one of them has to satisfy the property.

Random inputs are only half the story. When a property fails, the input that broke it is usually large and full of irrelevant detail. So the library **shrinks** it: it searches for the smallest input that still fails, and reports that instead. You get the bug in its simplest form, plus a token to replay that exact case:

```
Falsified after 12 tests and 34 shrinks (seed 3468194371).
  Minimal counterexample: [100]
  Original counterexample: [-3, 77, 100, 5, -19]
  Replay with: new CheckOptions { Replay = Replay.Parse("3468194371:11") }
```

### Finding properties

The hard part of property-based testing is not the tooling; it is noticing what is universally true of your code. Some patterns that turn up again and again:

- **Round trips.** Encode then decode, serialise then deserialise, save then load — you should get back what you started with. `Decode(Encode(s)) == s`.
- **Invariants.** Whatever the input, the output has some shape: a sort returns a list of the same length, a `Normalise` never returns a path with `..` in it, a balance never goes negative.
- **Idempotence.** Doing it twice is the same as doing it once: `Trim(Trim(s)) == Trim(s)`.
- **Commutativity and other algebra.** `Add(a, b) == Add(b, a)`; merging two configs then a third gives the same result as merging the second and third first.
- **A reference implementation.** Your fast, clever version should agree with the slow, obviously-correct one on every input. This is one of the most productive patterns: the "oracle" can be a naive loop, an old implementation you're replacing, or a call to a well-tested library.
- **It doesn't throw.** The weakest property, but a surprisingly good one: for all inputs of this shape, the code completes. Parsers, validators and anything handling untrusted input benefit from this alone.

Property-based tests don't replace example-based ones. Keep the handful of concrete examples that document what the code is for; add properties to cover the space you can't enumerate by hand.

## Installation

```shell
dotnet add package QuickCheck
```

## Generators

A `Generator<T>` describes how to produce values of `T`. Factories live on the static `Generate` class:

```csharp
Generate.Integer<int>()             // any int, biased towards small values and the extremes
Generate.Between(1, 10)             // inclusive range; any IBinaryInteger, spanning at most 64 bits
Generate.Boolean()
Generate.Char()                     // any UTF-16 code unit, biased towards printable ASCII
Generate.String(maxLength: 20)
Generate.Constant(42)
Generate.Elements("red", "green")   // pick one
Generate.Enum<DayOfWeek>()
Generate.OneOf(genA, genB)          // pick a generator uniformly
Generate.Frequency((9, common), (1, rare))
Generate.Tuple(genA, genB)
```

Generators compose. Combinators are extension members on `Generator<T>`, so LINQ query syntax works:

```csharp
var evens = Generate.Integer<int>().Select(x => x * 2);
var nonEmpty = Generate.String().Where(s => s.Length > 0);
var lists = Generate.Between(0, 100).List(minLength: 1, maxLength: 10);
var arrays = Generate.Boolean().Array();
var maybe = Generate.String().OrNull();          // reference types
var maybeInt = Generate.Integer<int>().Nullable(); // value types

// Dependent generation: a slice whose bounds lie inside its array.
var slices =
    from array in Generate.Integer<int>().Array(minLength: 1)
    from start in Generate.Between(0, array.Length - 1)
    from length in Generate.Between(0, array.Length - start)
    select (array, start, length);
```

For your own types, build a generator out of existing ones. `Generate.From` hands you a `ChoiceSource` to draw from:

```csharp
Generator<Money> money = Generate.From(source =>
    new Money(source.Draw(Generate.Between(0L, 1_000_000L)), source.Draw(Generate.Elements("GBP", "USD"))));
```

A generator built this way shrinks just as well as the built-in ones: shrinking works on the choices drawn from the `ChoiceSource`, not on the finished value, so it needs no knowledge of `Money` (see [How shrinking works](#how-shrinking-works)).

Use `generator.Sample(count, seed)` to eyeball what a generator produces before you rely on it.

## Properties

`Property.ForAll` pairs one to three generators with a body. A body that returns `bool` fails on `false`; a `void` body fails by throwing — so ordinary test assertions work inside it:

```csharp
Property.ForAll(Generate.Integer<int>(), Generate.Integer<int>(), (a, b) => a + b == b + a).Assert();

Property.ForAll(Generate.String(), s =>
{
    var roundTripped = Decode(Encode(s));
    Assert.Equal(s, roundTripped);
}).Assert();
```

- `Assert(options)` throws `PropertyFailedException` on failure — use it in tests. The message is the report shown above.
- `Check(options)` returns a `PropertyResult<T>` with the outcome, seed, counterexamples, and shrink statistics, without throwing.
- `Property.Assume(condition)` discards the current example when a precondition doesn't hold. Prefer a generator that only produces valid inputs; a property that discards too much is reported as `Exhausted` rather than silently passing on a handful of examples.

`CheckOptions` controls the run: `RunCount` (default 100), `Seed`, `Replay` (re-run one specific failing example), `MaxShrinkAttempts`, and `MaxDiscardRatio`. Both `Assert` and `Check` also take a `CancellationToken` after the options; it aborts the check between examples and between shrink attempts, throwing rather than reporting a result. The body is not given the token, so a long-running body runs to completion — but a body that throws `OperationCanceledException` while that token is cancelled aborts the check rather than being recorded as a counterexample.

Bodies can be asynchronous. `ForAll` with a `Task`-returning body gives an `AsyncProperty<T>` with `CheckAsync` and `AssertAsync`; examples are awaited one at a time so shrinking stays deterministic.

```csharp
await Property.ForAll(Generate.String(), async s => Assert.Equal(s, await RoundTripAsync(s))).AssertAsync();
```

Await the result: an `AssertAsync()` left un-awaited in a non-`async` test method compiles without a warning and the test passes whatever the property does.

### Reproducing failures

Every check is driven by a seed, which is printed in the report. The library owns its random number generator, so the same seed produces the same examples on every machine and runtime. To pin down a failure while you fix it, pass the replay token from the report:

```csharp
Property.ForAll(generator, body).Assert(new CheckOptions { Replay = Replay.Parse("3468194371:11") });
```

That runs only the failing example (and shrinks it as usual). Once fixed, drop the option and the test goes back to exploring fresh inputs each run.

## How shrinking works

During generation, every decision a generator makes — how long a list is, which branch of `OneOf` was taken, what an integer's value is — is recorded as an integer *choice*, with `0` always meaning the simplest option. A generated value is entirely a function of that choice sequence.

When an example fails, the shrinker doesn't try to edit the value; it edits the choice sequence — deleting spans, zeroing them, binary-searching individual choices towards zero, shrinking equal choices together, and moving value between numeric pairs — then replays the generator on the edited sequence and keeps any candidate that still fails *in the same way* (same exception type). Because shrinking only ever moves the sequence strictly towards "simpler", it always terminates; and because it works below the level of values, `Select`, `Where`, `SelectMany` and any custom generator get shrinking for free.

## Resources

- [Haskell's QuickCheck](https://hackage.haskell.org/package/QuickCheck), the original property-based testing library, and the paper that introduced it: [*QuickCheck: A Lightweight Tool for Random Testing of Haskell Programs*](https://www.cs.tufts.edu/~nr/cs257/archive/john-hughes/quick.pdf)
- [Hypothesis](https://hypothesis.readthedocs.io/), whose choice-sequence approach to shrinking this library follows
