# QuickCheck

Property-based testing for .NET.

This package is the core library containing generators, properties, shrinking and reporting. It has no test framework dependency, so can be used anywhere. If you use xUnit v3, the [QuickCheck.Xunit.v3](https://www.nuget.org/packages/QuickCheck.Xunit.v3) package adds a `[Property]` attribute that generates a test method's parameters for you.

- [Install](#install)
- [First property](#first-property)
- [Generators](#generators)
- [Properties](#properties)
- [Reproducing failures](#reproducing-failures)
- [Statistics](#statistics)
- [How shrinking works](#how-shrinking-works)

## Install

```shell
dotnet add package QuickCheck
```

## First property

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

The test incodes the property that _for all lists of integers, reversing twice gives back the original._ Each run tries 100 generated lists, including empty, long and full of extreme values.

When a property test fails the input that broke it is often large and contains irrelevant detail so QuickCheck shrinks it and reports the smallest input it can find that still fails:

```
Falsified after 12 tests and 34 shrinks (seed 3468194371).
  Minimal counterexample: [100]
  Original counterexample: [-3, 77, 100, 5, -19]
  Replay with: new CheckOptions { Replay = Replay.Parse("3468194371:11") }
```

## Generators

A `Generator<T>` describes how to produce values of `T`. Factories live on the static `Generate` class:

```csharp
Generate.Integer<int>()             // any int, biased towards small values and the extremes
Generate.Between(1, 10)             // inclusive range; any IBinaryInteger, spanning at most 64 bits
Generate.FloatingPoint<double>()    // any double: NaN, the infinities, -0 and subnormals included, short values most often
Generate.FloatingPoint(0.0, 1.0)    // inclusive range, never NaN; any IFloatingPointIeee754 (float, Half, NFloat)
Generate.Boolean()
Generate.Char()                     // any UTF-16 code unit, biased towards printable ASCII
Generate.String(maxLength: 20)      // from Char(); or String(Generate.Between('a', 'z')) for an alphabet
Generate.Constant(42)
Generate.Elements("red", "green")   // pick one
Generate.Enum<DayOfWeek>()
Generate.OneOf(genA, genB)          // pick a generator uniformly
Generate.Frequency((9, common), (1, rare))
Generate.Tuple(genA, genB)
Generate.Dictionary(keys, values)   // distinct keys; see Collection sizes
Generate.DateTime()                 // mostly 1900-2100, often round times, sometimes the bounds; DateTime(min, max) for a range
Generate.DateOnly()                 // mostly 1900-2100, sometimes the bounds
Generate.TimeOnly()                 // often round, sometimes the bounds
Generate.DateTimeOffset()           // any whole-minute offset, mostly whole hours; sometimes the bounds verbatim
Generate.DateTimeOffset(offset)     // every instant that one offset can represent
Generate.TimeSpan()                 // either sign; whole ticks, ms, seconds, minutes, hours or days
Generate.Guid()
```

The factories are named after their types, so in a file with `using static QuickCheck.Generate;` the names `DateTime`, `TimeSpan`, `Guid`, `String` and `Enum` resolve to the factories in expression position: `DateTime.UtcNow` or `Guid.NewGuid()` fail to compile there. Call the factories through `Generate.` instead, or write `System.DateTime.UtcNow` where you need both.

Generators compose. Combinators are extension members on `Generator<T>` which means LINQ query syntax works:

```csharp
var evens = Generate.Integer<int>().Select(x => x * 2);
var nonEmpty = Generate.String().Where(s => s.Length > 0);
var lists = Generate.Between(0, 100).List(minLength: 1, maxLength: 10);
var arrays = Generate.Boolean().Array();
var memory = Generate.Integer<byte>().Memory();
var sets = Generate.Between(0, 100).HashSet(minLength: 1, maxLength: 8);
var maybe = Generate.String().OrNull();          // reference types; null 10% of the time
var maybeInt = Generate.Integer<int>().Nullable(); // value types

// Dependent generation: a slice whose bounds lie inside its array.
var slices =
    from array in Generate.Integer<int>().Array(minLength: 1)
    from start in Generate.Between(0, array.Length - 1)
    from length in Generate.Between(0, array.Length - start)
    select (array, start, length);
```

### Custom types

Build a generator for your own types out of existing ones. `Generate.From` hands you a `ChoiceSource` to draw from:

```csharp
Generator<Money> money = Generate.From(source =>
    new Money(source.Draw(Generate.Between(0L, 1_000_000L)), source.Draw(Generate.Elements("GBP", "USD"))));
```

You get shrinking here for free. Shrinking works on the choices drawn from the `ChoiceSource`, not on the resulting value so needs no knowledge of `Money` here (see [How shrinking works](#how-shrinking-works)).

A type can declare its own default generator by implementing `IArbitrary<T>`, a single static `Arbitrary` property. That gives the generator a conventional home (`Property.ForAll(Money.Arbitrary, ...)`), and the xUnit adapter picks it up for parameters of that type:

```csharp
public readonly record struct Money(decimal Amount) : IArbitrary<Money>
{
    public static Generator<Money> Arbitrary { get; } =
        Generate.Between(0, 1_000_000).Select(cents => new Money(cents / 100m));
}
```

### Recursive types

A generator that refers to itself needs `Generate.Deferred`, which takes a factory and only calls it on the first draw, so a generator method can call itself without recursing forever at construction. Weight the base case so that generation terminates:

```csharp
abstract record Expression;
sealed record Literal(int Value) : Expression;
sealed record Add(Expression Left, Expression Right) : Expression;

static Generator<Expression> Expressions() => Generate.Frequency(
    (3, Generate.Integer<int>().Select(Expression (value) => new Literal(value))),
    (1, Generate.Deferred(() => Generate.Tuple(Expressions(), Expressions()))
            .Select(Expression (pair) => new Add(pair.Item1, pair.Item2))));
```

### Floating point

`Generate.FloatingPoint<T>()` covers every `IFloatingPointIeee754<T>` with a `MinValue` and `MaxValue` (`double`, `float`, `Half`, `NFloat`). A value is drawn as an integer significand times a power of two, small exponents and short significands most often, so values print short: about 60% of doubles are integers and about a third have an exponent within ±7 (values like 0.375, 1.5 and 96). The rest spread over the whole exponent range, so about 8% are above 1e100, 2% are subnormal and 2.5% are non-finite.

The full-range generator forces NaN, both infinities, `MinValue`, `MaxValue`, `Epsilon` of either sign and -0.0 one draw in sixteen between them. A bounded range forces its bounds and whichever of those lie within it, never produces NaN, and produces an infinity only when that bound is infinite. Bounds are sign-aware for zero: `FloatingPoint(0.0, 10.0)` never yields -0.0. The sign is drawn uniformly when the range spans zero, however narrow a side is, so `FloatingPoint(-0.0, 10.0)`, whose negative side holds nothing but -0.0, yields -0.0 half the time.

```csharp
var probabilities = Generate.FloatingPoint(0.0, 1.0);
var finite = Generate.FloatingPoint(double.MinValue, double.MaxValue);
```

Shrinking lowers the exponent before the significand, so a shrunk value is an integer or a short fraction: the minimal counterexample of `Generate.FloatingPoint<double>()` is `0`, of `FloatingPoint(0.3, 0.9)` is `0.5`, and a failure that depends on a threshold can end on a round value just past it (`x <= 100` ends on 101 for most seeds and 128 for some).

Reports print floating-point values in their shortest round-trip form (`0.1`, `5E-324`, `-0`, `NaN`, `Infinity`).

### Dates and times

`DateTime`, `DateTimeOffset`, `DateOnly` and `TimeOnly` are drawn component by component rather than as a uniform tick count, so months, days and hours are uniform and shrinking reads naturally: the year shrinks towards 2000, the other components towards their minimum, and a time drops its detail (ticks, then milliseconds, seconds, minutes) before it shrinks what is left. The year is in 1900 to 2100 three draws in four and anywhere in 1 to 9999 otherwise, and a time is midnight or a whole hour, minute, second or millisecond about four draws in five. The minimal counterexample of `Generate.DateTime()` is `2000-01-01T00:00:00`.

Drawing each component on its own means a particular value such as the upper bound almost never appears by chance, so one draw in sixteen is forced to the range's lower or upper bound instead (`DateTimeOffset` forces its bounds verbatim, so a bound written at `+05:30` appears at `+05:30`). A forced bound goes through the same components as any other value, so it shrinks the same way, and an off-by-one at `max` fails within a few dozen examples rather than never.

`Generate.DateTime(kind)` gives every value the one `DateTimeKind` (`Unspecified` by default), and `Generate.DateTime(min, max)` takes it from the bounds, which have to agree. So `Generate.DateTime(utcMin, utcMax)` produces UTC values, and a system under test that calls `ToUniversalTime` on them stays inside the window you asked for. For a mix of kinds, draw the kind first:

```csharp
var anyKind = Generate.Enum<DateTimeKind>().SelectMany(kind => Generate.DateTime(kind));
```

`Generate.DateTimeOffset(min, max)` compares its bounds as instants and draws the offset independently of them: any whole minute from -14:00 to +14:00 that keeps the local clock time inside `DateTime`'s range, whole hours three draws in four. Pass an offset to fix it instead:

```csharp
var inIndia = Generate.DateTimeOffset(TimeSpan.FromHours(5.5));
```

A fixed offset trims whichever end of the range would push the local clock time past `DateTime`'s: `inIndia` stops 5:30 before `DateTimeOffset.MaxValue`, where the local time reaches `DateTime.MaxValue`, and a negative offset trims the early end instead. An offset that leaves nothing of `min`..`max` throws at the call site rather than part way into a run.

`Generate.TimeSpan()` picks a unit (ticks, milliseconds, seconds, minutes, hours or days) and then a whole number of it of either sign, small counts most often, so spans of every scale appear and a shrunk span is a round one.

Reports print dates and times in ISO form down to the tick (`2000-01-01T00:00:00.0000001`), since the default `DateTime` and `TimeOnly` formats would hide the fraction.

### Edge values

A generator forces only the values it can derive from its bounds and its type: the ends of the range, the type's `MinValue` and `MaxValue`, zero or the shrink target, and the ends of each component's natural range. It never forces a value because code is known to get it wrong, such as a date that breaks hand-rolled leap-year code or an epoch some code treats as unset, so each generator's doc comment can name everything it forces. Values that matter to your domain are yours to add, and `Generate.Frequency` is the way:

```csharp
var timestamps = Generate.Frequency(
    (15, Generate.DateTime()),
    (1, Generate.Constant(DateTime.UnixEpoch)));
```

A failure found through the constant branch minimises to that constant rather than to a component-shrunk value, which for a probe is usually the answer you wanted.

### Collection sizes

`List`, `Array`, `Memory`, `String`, `HashSet`, and `Dictionary` default to lengths between 0 and 64, but they do not draw uniformly from that range. They aim for an average length of about `minLength + 5` (or `2 * minLength`, if that is larger), never more than the middle of the range, and longer collections get rarer geometrically. This keeps examples small enough to run and shrink quickly. When length matters to your property, raise `minLength` rather than `maxLength` and use [`Classify`](#statistics) to check the distribution you are actually getting.

For `HashSet` and `Dictionary` the length counts distinct elements or keys, under the type's default equality. Each new element or entry draws up to ten candidates, skipping duplicates (and null keys). Once the collection holds `minLength` entries, ten skips end it, so a small domain caps the size rather than failing: `Generate.Boolean().HashSet()` stops at two elements and never discards. Before that, ten skips discard the example, so a `minLength` above the number of distinct values the generator can produce discards every example.

### Sampling

`generator.Sample(count, seed)` returns what a generator produces so you can view the output of your generators.

```csharp
foreach (var example in slices.Sample(count: 5, seed: 1))
{
    Console.WriteLine(ValueFormatter.Format(example));
}
```

## Properties

`Property.ForAll` pairs one to three generators with a body. The body can indicate failure either by returning false or throwing an exception.

```csharp
Property.ForAll(Generate.Integer<int>(), Generate.Integer<int>(), (a, b) => a + b == b + a).Assert();

Property.ForAll(Generate.String(), s =>
{
    var roundTripped = Decode(Encode(s));
    Assert.Equal(s, roundTripped);
}).Assert();
```

Two ways to run a property:

- `Assert(options)` throws `PropertyFailedException` on any outcome other than `Passed`. The exception message is the report shown above.
- `Check(options)` returns a `PropertyResult<T>` without throwing with some extra useful information. `result.ThrowIfFailed()` turns it back into the exception, and `result.ToString()` is the report.

### Preconditions

`Property.Assume(condition)` discards the current example when a precondition does not hold. Prefer a generator that only produces valid inputs. Note that a property that discards too much is reported as `Exhausted` rather than passing on only a few examples.

### Options

Configure via `CheckOptions`. Only set what you need:

| Option               | Default   | Effect                                                                                                                    |
| -------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------- |
| `RunCount`           | 100       | Examples to try before passing; the fewest examples that must pass when `CoverageConfidence` is set.                      |
| `Seed`               | random    | Fixes the example sequence; the report prints the seed used.                                                              |
| `Replay`             | none      | Runs only one specific example from an earlier report; see [Reproducing failures](#reproducing-failures).                 |
| `MaxDiscardRatio`    | 10        | Discards allowed per passed example before the check is `Exhausted`.                                                      |
| `MaxShrinkAttempts`  | 10,000    | Candidates the shrinker may try; 0 disables shrinking.                                                                    |
| `MaxShrinkWork`      | 5,000,000 | Total choices shrinking may replay, which bounds the work one very large counterexample can cost.                         |
| `CoverageConfidence` | none      | Checks `Cover` requirements to a stated certainty and fails on a known miss; see [Checking coverage](#checking-coverage). |

### Cancellation

`Assert` and `Check` take a `CancellationToken`. It aborts the check between examples and between shrink attempts, throwing rather than reporting a result. The body is not given the token, so a long-running body runs to completion. A body that throws `OperationCanceledException` while that token is cancelled aborts the check rather than being recorded as a counterexample.

### Asynchronous bodies

`ForAll` with a `Task`-returning body gives an `AsyncProperty<T>` with `CheckAsync` and `AssertAsync`; examples are awaited one at a time so shrinking stays deterministic.

```csharp
await Property.ForAll(Generate.String(), async s => Assert.Equal(s, await RoundTripAsync(s))).AssertAsync();
```

Mame sure to await the result.

## Reproducing failures

Every check has a seed which is printed in the report. The same seed produces the same examples on every machine and runtime. To replay a failure, pass the replay token from the report:

```csharp
Property.ForAll(generator, body).Assert(new CheckOptions { Replay = Replay.Parse("3468194371:11") });
```

## Statistics

A property can pass while exercising almost nothing. Four methods report what each example exercised and the passing report prints the distribution:

- `Property.Classify(condition, label)` counts the example under `label` when `condition` holds. `Property.Label(label)` counts it unconditionally.
- `Property.Collect(name, value)` counts the example under `value` in a table called `name`.
- `Property.Cover(condition, minimumPercent, label)` counts like `Classify` and states that at least `minimumPercent` of the examples should hit the label. A shortfall prints as a warning, `Only 3% label, but required 20%`, under the headline of a passing report; see [Checking coverage](#checking-coverage) to make it fail the check.

```csharp
Property.ForAll(Generate.Integer<int>().List(), list =>
{
    Property.Classify(list.Count is 0, "empty");
    Property.Cover(list.Count >= 5, 20, "five or more");
    Property.Collect("sign of first", list.Count is 0 ? "none" : Math.Sign(list[0]).ToString());
    Assert.Equal(list, list.AsEnumerable().Reverse().Reverse());
}).Assert();
```

```
Passed 100 tests (seed 1).
  34% five or more (required 20%)
  19% empty
  sign of first:
    43% -1
    37% 1
    19% none
    1% 0
```

### Checking coverage

A `Cover` shortfall is only a warning by default because a plain threshold over one seed's `RunCount` examples fails about half the time when the real rate equals the minimum. To make the requirement an assertion, set `CoverageConfidence`, which is Haskell QuickCheck's [`checkCoverage`](https://hackage-content.haskell.org/package/QuickCheck-2.18.0.0/docs/Test-QuickCheck.html#v:checkCoverage):

```csharp
Property.ForAll(generator, body).Assert(new CheckOptions { CoverageConfidence = Confidence.Default });
```

The check then treats `RunCount` as the fewest examples that must pass and looks at the coverage at `RunCount` and after 100, 200, 400, … passes until every requirement is known to be met or one is known to be missed. A run can therefore be much longer than `RunCount`, and a shortfall known early fails with `InsufficientCoverage` before `RunCount` is reached. "Known" means to the `Certainty` of the confidence, one wrong decision in a billion checks by default.

A true rate near the minimum is the slowest to decide, and a small minimum needs far more examples than a large one: with the defaults, a rate at a 50% minimum decides after about 6,400 examples, one in the middle of the tolerance band after about 25,600, and a rate at a 1% minimum takes around a million. A rate between `Tolerance` (0.9 by default) times the minimum and the minimum may be accepted or rejected, so state the minimum you need rather than the rate you expect. [Haskell QuickCheck's rule](https://hackage-content.haskell.org/package/QuickCheck-2.18.0.0/docs/Test-QuickCheck.html#t:Confidence) for `Certainty` is 100 times the number of `Cover` calls in the suite, times how often the suite is expected to run, for a 1% chance of a wrong failure over the project's lifetime.

Each look compares a [Wilson score interval](https://doi.org/10.1080/01621459.1927.10502953) for the requirement with the minimum, at a z-score from [Acklam's inverse normal approximation](https://web.archive.org/web/20151110174102/http://home.online.no/~pjacklam/notes/invnorm/), and spends half the error budget the previous look left, so the certainty covers the run however many looks it takes; QuickCheck spends the whole budget at every look, so its certainty holds per look. The interval is Wilson's rather than the normal approximation, whose accuracy is erratic at exactly the rates and counts a coverage check works with, as [Brown, Cai and DasGupta](https://doi.org/10.1214/ss/1009213286) measure.

## How shrinking works

During generation, every decision a generator makes is recorded as an integer _choice_, with `0` always meaning the simplest option. A generated value is entirely a function of that choice sequence.

When an example fails, the shrinker edits the choice sequence by deleting spans, zeroing them, binary-searching individual choices towards zero, shrinking equal choices together, and moving value between numeric pairs. It then then replays the generator on the edited sequence and keeps any candidate that still fails in the same way. This is the approach [Hypothesis](https://hypothesis.readthedocs.io/) takes, and its papers and documentation are the best place to read more.
