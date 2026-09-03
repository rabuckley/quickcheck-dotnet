# QuickCheck

Property-based testing for .NET.

This package is the core library containing generators, properties, shrinking and reporting. It has no test framework dependency, so can be used anywhere. If you use xUnit v3, the [QuickCheck.Xunit.v3](https://www.nuget.org/packages/QuickCheck.Xunit.v3) package adds a `[Property]` attribute that generates a test method's parameters for you.

- [Install](#install)
- [First property](#first-property)
- [Generators](#generators)
- [Properties](#properties)
- [Stateful testing](#stateful-testing)
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

The test encodes the property that _for all lists of integers, reversing twice gives back the original._ Each run tries 100 generated lists, including empty, long and full of extreme values.

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
Generate.Decimal()                  // any decimal at any scale; Decimal(min, max) for a range
Generate.Boolean()
Generate.Char()                     // any UTF-16 code unit, biased towards printable ASCII
Generate.String(maxLength: 20)      // from Char(); or String(Generate.Between('a', 'z')) for an alphabet
Generate.Constant(42)
Generate.Elements("red", "green")   // pick one
Generate.Enum<DayOfWeek>()
Generate.OneOf(genA, genB)          // pick a generator uniformly
Generate.Frequency((9, common), (1, rare))
Generate.Tuple(genA, genB)
Generate.Build(genA, genB, (a, b) => new Foo(a, b))   // one value from each, combined; two to eight generators
Generate.Sequence(genA, genB)       // one value from each, as an array; any number of generators of one type
Generate.Dictionary(keys, values)   // distinct keys; see Collection sizes
Generate.CommandSequence(() => model, model => commands)   // operations against a model; see Stateful testing
Generate.DateTime()                 // mostly 1900-2100, often round times, sometimes the bounds; DateTime(min, max) for a range
Generate.DateOnly()                 // mostly 1900-2100, sometimes the bounds
Generate.TimeOnly()                 // often round, sometimes the bounds
Generate.DateTimeOffset()           // any whole-minute offset, mostly whole hours; sometimes the bounds verbatim
Generate.DateTimeOffset(offset)     // every instant that one offset can represent
Generate.TimeSpan()                 // either sign; whole ticks, ms, seconds, minutes, hours or days
Generate.Guid()
```

The factories are named after their types, so in a file with `using static QuickCheck.Generate;` the names `DateTime`, `TimeSpan`, `Guid`, `Decimal`, `String` and `Enum` resolve to the factories in expression position: `DateTime.UtcNow`, `Guid.NewGuid()` or `Decimal.MaxValue` fail to compile there. Call the factories through `Generate.` instead, or write `System.DateTime.UtcNow` or `decimal.MaxValue` where you need both.

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

Build a generator for your own types out of generators for their members. `Generate.Build` draws one value from each member generator, in order, and passes them to a function:

```csharp
Generator<Money> money = Generate.Build(
    Generate.Between(0L, 1_000_000L),
    Generate.Elements("GBP", "USD"),
    (amount, currency) => new Money(amount, currency));
```

Overloads take two to eight generators. For more, nest a `Build`, or use `Generate.Sequence`, which draws one value from each of any number of generators of one type into an array.

The members are drawn independently. When a relation ties two of them together, such as a lower bound that must not exceed an upper bound, filter the pair before building it rather than guarding in the constructor, so that the shrinker never proposes a pair the type refuses:

```csharp
Generator<Interval> intervals = Generate.Tuple(Generate.Between(0, 100), Generate.Between(0, 100))
    .Where(pair => pair.Item1 <= pair.Item2)
    .Select(pair => new Interval(pair.Item1, pair.Item2));
```

When a later draw depends on an earlier value, `Generate.From` hands you a `ChoiceSource` to draw from in whatever order the type needs:

```csharp
Generator<Money> roundMoney = Generate.From(source =>
{
    var currency = source.Draw(Generate.Elements("GBP", "JPY"));
    var unit = currency == "JPY" ? 1L : 100L;
    return new Money(unit * source.Draw(Generate.Between(0L, 10_000L)), currency);
});
```

You get shrinking here for free. Shrinking works on the choices drawn from the `ChoiceSource`, not on the resulting value, so it needs no knowledge of `Money` (see [How shrinking works](#how-shrinking-works)).

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
    (1, Generate.Deferred(() => Generate.Build(
            Expressions(), Expressions(), Expression (left, right) => new Add(left, right)))));
```

### Floating point

`Generate.FloatingPoint<T>()` covers every `IFloatingPointIeee754<T>` with a `MinValue` and `MaxValue` (`double`, `float`, `Half`, `NFloat`), and `Generate.Decimal()` covers `decimal`. A value is drawn as an integer significand times a power of the radix (2, or 10 for `decimal`), small exponents and short significands most often, so values print short: about 60% of doubles are integers and about a third have an exponent within ±7 (values like 0.375, 1.5 and 96). The rest spread over the whole exponent range, so about 8% are above 1e100, 2% are subnormal and 2.5% are non-finite.

The full-range generator forces NaN, both infinities, `MinValue`, `MaxValue`, `Epsilon` of either sign and -0.0 one draw in sixteen between them. A bounded range forces its bounds and whichever of those lie within it, never produces NaN, and produces an infinity only when that bound is infinite. Bounds are sign-aware for zero: `FloatingPoint(0.0, 10.0)` never yields -0.0. The sign is drawn uniformly when the range spans zero, however narrow a side is, so `FloatingPoint(-0.0, 10.0)`, whose negative side holds nothing but -0.0, yields -0.0 half the time.

```csharp
var probabilities = Generate.FloatingPoint(0.0, 1.0);
var finite = Generate.FloatingPoint(double.MinValue, double.MaxValue);
var prices = Generate.Decimal(0.01m, 1000m);
```

Shrinking lowers the exponent before the significand, so a shrunk value is an integer or a short fraction: the minimal counterexample of `Generate.FloatingPoint<double>()` is `0`, of `FloatingPoint(0.3, 0.9)` is `0.5`, and a failure that depends on a threshold can end on a round value just past it (`x <= 100` ends on 101 for most seeds and 128 for some). `Generate.Decimal()` draws the scale as well, so cohort members such as `1.0m` and `1.00m` both appear, and it shrinks towards `0m`.

Reports print floating-point values in their shortest round-trip form (`0.1`, `5E-324`, `-0`, `NaN`, `Infinity`) and decimals with their scale (`1.00`).

### Dates and times

`DateTime`, `DateTimeOffset`, `DateOnly` and `TimeOnly` are drawn component by component rather than as a uniform tick count, so shrinking reads naturally: the year shrinks towards 2000, the other components towards their minimum, and a time drops its detail (ticks, then milliseconds, seconds, minutes) before it shrinks what is left. The year is in 1900 to 2100 three draws in four and anywhere in 1 to 9999 otherwise, and a time is midnight or a whole hour, minute, second or millisecond five draws in six (four in five for `TimeOnly`). The minimal counterexample of `Generate.DateTime()` is `2000-01-01T00:00:00`. Dates are uniform over the range: a year or month gets its share of the range's days rather than an equal share of draws, so an unforced draw of `Generate.DateTime(2024-01-01, 2025-01-01)` lands on 2025 about one time in 370, not one in two (the forced bounds described below add one more in 32). A `DateTime` bound gets a whole day's weight even where only part of that day is in range, so a bound date is at most a day's share too common.

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

Either way, a generator that throws ends the check like any other failure rather than escaping it: the outcome is `GenerationFailed`, `result.GenerationException` is what it threw, and the report carries the seed and a replay token for the draw.

### Preconditions

`Property.Assume(condition)` discards the current example when a precondition does not hold. Prefer a generator that only produces valid inputs. Note that a property that discards too much is reported as `Exhausted` rather than passing on only a few examples, with the estimated discard rate in the report.

A generator is held to the same rule: it must return a value or discard the example, with `Property.Assume` or `Where`. Any other exception it throws is a defect in the generator, not an input the check tolerates, so it ends the check.

### Explicit examples

`Example(value)` pins a value the property is always checked on, whatever the generators produce. Pinned values are checked first, in the order you add them, and the first one to fail ends the check.

```csharp
Property
    .ForAll(Generate.Integer<int>(), Generate.Integer<int>(), (a, b) => _ = a / b)
    .Example((0, 0))
    .Assert();
```

This is how you keep a found bug checked. A replay token names an example by its position in a seeded stream, so it points at a different input as soon as a generator changes shape: add an edge value, widen a range, or reorder two draws, and the test keeps passing without ever checking the bug it was written for. A pinned value keeps testing the input the failure was found on. A property over several generators is a property over a tuple, so pin a tuple.

A report formats its counterexample for reading, not for pasting. Integers, `bool`, `char`, strings, tuples and collections print as C# you can copy into `Example` as it stands. A record, `DateTime` or `decimal` does not, and neither do the floating-point edges: `NaN` and the infinities print as `NaN`, `Infinity` and `-Infinity`, and `-0.0` prints as `-0`, which reads back as positive zero. Write the literal yourself for those.

An explicit example:

- is checked on top of `RunCount` rather than out of it, so pinning one never shortens the generated run;
- is reported as given if it fails, unshrunk (there are no choices behind a literal for the shrinker to reduce) and with no replay token, so `result.Replay` is null and `result.TestsRun` is 0; `result.IsFalsified && result.Minimal.IsExplicit` is how you tell that failure from a generated one;
- contributes nothing to the `Classify` statistics;
- is skipped, and reported as skipped, when `Property.Assume` discards it;
- is never checked against the generator, so it may be a value the generator's range excludes.

`CheckOptions.Replay` and pinned values are mutually exclusive: a replay checks only the example its token names, so `Check` throws `ArgumentException` rather than leave the pins unchecked.

### Options

Configure via `CheckOptions`. Only set what you need:

| Option               | Default   | Effect                                                                                                                    |
| -------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------- |
| `RunCount`           | 100       | Examples to try before passing; the fewest examples that must pass when `CoverageConfidence` is set.                      |
| `Seed`               | random    | Fixes the example sequence; the report prints the seed used.                                                              |
| `Replay`             | none      | Runs only one specific example from an earlier report; see [Reproducing failures](#reproducing-failures).                 |
| `MaxDiscardRatio`    | 10        | Discards allowed per passed example before the check is `Exhausted`; it gives up sooner once that outcome is certain.     |
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

Make sure to await the result.

## Stateful testing

A property over one value says little about a system with state. Whether `Delete` is valid depends on the `Put`s before it, and what `Get` should return depends on both. Stateful testing makes the property range over a sequence of operations. You describe each operation as a command against a model, a simplification of the system that is enough to say which commands are valid and what the system should answer, and the property runs a generated sequence against the real system, comparing as it goes. The model is not a second implementation: a `Dictionary` is a fine model of a database.

A command implements `ICommand<TModel, TSystem>` with three members. `Precondition(model)` says whether the command may follow that model state; the default is always. `Update(model)` returns the model state after the command, the same object when the model is mutable. `Run(model, system)` executes the command against the real system and asserts what it observes, given the model state before it. Records are the natural shape because a failure report prints them with their members:

```csharp
using Command = QuickCheck.ICommand<Dictionary<int, int>, Store>;

sealed record Put(int Key, int Value) : Command
{
    public Dictionary<int, int> Update(Dictionary<int, int> model) { model[Key] = Value; return model; }
    public void Run(Dictionary<int, int> model, Store store) => store.Put(Key, Value);
}

sealed record Get(int Key) : Command
{
    public Dictionary<int, int> Update(Dictionary<int, int> model) => model;
    public void Run(Dictionary<int, int> model, Store store) =>
        Assert.Equal(model.GetValueOrDefault(Key), store.Get(Key));
}

sealed record Delete(int Key) : Command
{
    public bool Precondition(Dictionary<int, int> model) => model.ContainsKey(Key);
    public Dictionary<int, int> Update(Dictionary<int, int> model) { model.Remove(Key); return model; }
    public void Run(Dictionary<int, int> model, Store store) => store.Delete(Key);
}

static Generator<Command> Next(Dictionary<int, int> model) => Generate.Frequency(
    (3, Generate.Build(Generate.Between(0, 3), Generate.Between(0, 100), Command (key, value) => new Put(key, value))),
    (2, Generate.Between(0, 3).Select(Command (key) => new Get(key))),
    (1, Generate.Between(0, 3).Select(Command (key) => new Delete(key))));

[Fact]
public void Store_agrees_with_a_dictionary() =>
    Property.ForAll(
        Generate.CommandSequence(() => new Dictionary<int, int>(), Next),
        sequence => sequence.Run(new Store(), (model, store) => Assert.Equal(model.Count, store.Count)))
    .Assert();
```

`Generate.CommandSequence(initialModel, command, maxLength)` generates a sequence by advancing the model alone. For each step it calls `command` with the current model, draws a command from the generator it returns, redraws up to ten times while the precondition rejects it, and applies `Update`. `Run` is never called while generating. Ten rejections discard the example, so a specification with a state that offers no valid command shows up as discards rather than as short sequences; to end a sequence on purpose in a terminal state, return `null` from `command`. A sequence nearly always has `maxLength` commands (50 by default), because each step continues with probability 1 − 2⁻¹⁶, and the shrinker rather than the length draw finds the shortest failing prefix.

`sequence.Run(system, invariant)` runs the commands in order against a fresh model from `initialModel`: `Run`, then `Update`, then the invariant, which is also checked before the first command. It returns the final model.

Suppose `Put` ignores a write to a key that already holds a value. The 50-command original shrinks to three:

```
Falsified after 1 tests and 56 shrinks (seed 2024).
  Minimal counterexample: Put { Key = 0, Value = 0 }
    Put { Key = 0, Value = 1 }
    Get { Key = 0 }
    threw Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ
    Expected: 1
    Actual:   0
  Original counterexample: Put { Key = 3, Value = 4 }
    Get { Key = 0 }
    Put { Key = 3, Value = 70 }
    …
  Replay with: new CheckOptions { Replay = Replay.Parse("2024:0") }
```

Shrinking deletes the commands the failure does not need and shrinks the arguments of the ones that remain. Every candidate is generated afresh against the model, so a precondition is re-evaluated in the shrunk state: `Delete` never runs on a key the model does not hold, however the sequence around it has been cut.

### Writing commands

**Return type.** `Generate.Between(0, 3).Select(key => new Get(key))` is a `Generator<Get>`, which is not a `Generator<Command>`. Name the return type on the lambda, `Command (key) => new Get(key)`, as the `Expression` example does.

**One shape in every state.** Have `command` return a generator of the same shape whatever the model holds. The shrinker deletes a step by removing its choices, and the steps after it are then regenerated from the choices they already had; a step whose layout depends on the model reads those choices as something else and the deletion is rejected. Where a command has no valid argument in some state, keep it in the `Frequency` and let its precondition reject it, or draw from a placeholder, rather than leaving it out. `Frequency` and `OneOf` shrink towards their first generator, so list the command that is always applicable first.

**Keys, not objects.** Every run of a sequence, including each replay while shrinking, starts from a new model, so a command may hold only what is equal across replays: a key, an id, or a value drawn from an immutable model. With a mutable model, never hold an object taken out of it. `Generate.Elements(model.Values)` into `Withdraw(Account Account, int Amount)` compiles and runs, but the account is the generation-time object, `Update` mutates it instead of the model being run, and the assertion fails on a nonsense minimum. Hold the key and look it up in the `model` argument of `Run`. For the same reason `initialModel` must return an equal model on every call.

### Handles and ids

A system that hands out handles (a file handle, a connection, a row id) cannot be modelled by holding them, because real results do not exist when the sequence is generated. Let the model assign sequential ids and let the system side map them to real handles as `Open` runs:

```csharp
sealed class Handles
{
    public int Next { get; set; }
    public List<int> Open { get; } = [];
}

sealed record Open : ICommand<Handles, Files>
{
    public Handles Update(Handles model) { model.Open.Add(model.Next); model.Next++; return model; }
    public void Run(Handles model, Files files) => files.ByModelId[model.Next] = files.Open();
}

sealed record Write(int Id) : ICommand<Handles, Files>
{
    public bool Precondition(Handles model) => model.Open.Contains(Id);
    public Handles Update(Handles model) => model;
    public void Run(Handles model, Files files) => files.Write(files.ByModelId[Id]);
}

static Generator<ICommand<Handles, Files>> Next(Handles model) => Generate.Frequency(
    (2, Generate.Constant<ICommand<Handles, Files>>(new Open())),
    (1, Generate.Elements(model.Open.AsEnumerable().Reverse().DefaultIfEmpty(-1))
        .Select(ICommand<Handles, Files> (id) => new Write(id))));
```

`Files` is the system under test with a `Dictionary<int, FileHandle>` from model id to real handle beside it. `Open` knows its id because `Run` sees the model before the command, where `Next` is the id `Update` is about to assign. The open ids are listed newest first because `Elements` shrinks towards its first item: a command that refers to the newest handle lets the shrinker delete the earlier `Open`s, where one that refers to the oldest keeps them all. The `-1` placeholder keeps the generator the same shape when nothing is open, and the precondition rejects it.

### Your own loop

When `Run` does not fit, iterate `sequence.Commands` yourself: a scheduler over several replicas with a channel to drop messages from, an asynchronous system, a system that must be torn down in a `finally`. The guarantees are the sequence's, not `Run`'s: every command satisfied its precondition in the model state before it, and shrinking finds the shortest failing prefix whatever loop runs it.

```csharp
foreach (var command in sequence.Commands)
{
    switch (command)
    {
        case Write write: primary.Append(write.Value); channel.Enqueue(write.Value); break;
        case Deliver: follower.Apply(channel.Dequeue()); break;
        case Drop: channel.Dequeue(); break;
    }
}
```

With the xUnit adapter, a `[Property]` parameter of type `CommandSequence<TModel, TSystem>` takes its generator from a `[Generator(nameof(...))]` attribute, as any other type does.

### Statistics over sequences

`Collect` and `Cover` count each example once, so a table of command names would read 100% for every kind. Count what matters per example:

```csharp
Property.ForAll(Generate.CommandSequence(() => new Dictionary<int, int>(), Next), sequence =>
{
    Property.Collect("length", sequence.Commands.Count < 50 ? "under 50" : "50");
    Property.Cover(sequence.Commands.Any(command => command is Delete), 50, "has a delete");
    sequence.Run(new Store());
}).Assert();
```

## Reproducing failures

Every check has a seed which is printed in the report. The same seed produces the same examples on every machine and runtime. To replay a failure, pass the replay token from the report:

```csharp
Property.ForAll(generator, body).Assert(new CheckOptions { Replay = Replay.Parse("3468194371:11") });
```

A token is only good for as long as every generator in the property draws the same choices in the same order, so it is a way to look at a failure now, not a way to keep checking it. To pin a failure for good, pin the failing input with [`Example`](#explicit-examples) instead.

## Statistics

A property can pass while exercising almost nothing. Four methods report what each example exercised and the passing report prints the distribution:

- `Property.Classify(condition, label)` counts the example under `label` when `condition` holds. `Property.Label(label)` counts it unconditionally.
- `Property.Collect(name, value)` counts the example under `value` in a table called `name`.
- `Property.Cover(condition, minimumPercent, label)` counts like `Classify` and states that at least `minimumPercent` of the examples should hit the label. A shortfall prints as a warning, `Only 3% label, but required 20% (the true rate is 1% to 8%)`, under the headline of a passing report; see [Checking coverage](#checking-coverage) to make it fail the check. The range is where the generator's real rate plausibly lies given these examples: when it covers the minimum the dip may be seed luck, when it sits wholly below the minimum the gap is real, and more runs narrow it.

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

Each look compares a [Wilson score interval](https://doi.org/10.1080/01621459.1927.10502953) for the requirement with the minimum, at a z-score from [Acklam's inverse normal approximation](https://web.archive.org/web/20151110174102/http://home.online.no/~pjacklam/notes/invnorm/), and spends half the error budget the previous look left, so the certainty covers the run however many looks it takes; QuickCheck spends the whole budget at every look, so its certainty holds per look. The interval is Wilson's rather than the normal approximation, whose accuracy is erratic at exactly the rates and counts a coverage check works with, as [Brown, Cai and DasGupta](https://doi.org/10.1214/ss/1009213286) measure. The range on the default warning line, which prints without `CoverageConfidence`, is the 95% equal-tailed credible interval of the Jeffreys posterior Beta(½, ½), which the same paper recommends alongside Wilson's interval; it is computed from the regularized incomplete beta function ([DLMF 8.17](https://dlmf.nist.gov/8.17)'s continued fraction, with a Lanczos log-gamma).

## How shrinking works

During generation, every decision a generator makes is recorded as an integer _choice_, with `0` always meaning the simplest option. A generated value is entirely a function of that choice sequence.

When an example fails, the shrinker edits the choice sequence by deleting spans, merging adjacent collections, zeroing spans, binary-searching individual choices towards zero, shrinking equal choices together, and moving value between numeric pairs. It then replays the generator on the edited sequence and keeps any candidate that still fails in the same way: the same exception type thrown from the same line of the property body (or of the command it ran), or the property returning `false` again. A candidate that fails a different way is not taken, so the minimal counterexample is for the bug the check found first. This is the approach [Hypothesis](https://hypothesis.readthedocs.io/) takes, and its papers and documentation are the best place to read more.
