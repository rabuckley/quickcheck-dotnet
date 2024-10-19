# QuickCheck for .NET

From the original [Haskell version](https://hackage.haskell.org/package/QuickCheck),

> QuickCheck is a library for random testing of program properties. The programmer provides a specification of the program, in the form of properties which functions should satisfy, and QuickCheck then tests that the properties hold in a large number of randomly generated cases.

This library aims to provide similar property testing functionality for .NET.

## Installation

QuickCheck is available as a [NuGet package](https://www.nuget.org/packages/QuickCheck/). You can add it to an existing project as you would any other public nuget package. Using the .NET CLI:

```shell
dotnet add package QuickCheck
```

## Example

Say I've written a function to reverse `Memory<int>`. A property of a working reverse function is that reversing a reversed sequence should give the original sequence. We can check that this is the case for our function using QuickCheck.

```csharp
using QuickCheck;

// The function to test
static Memory<T> Reverse<T>(Memory<T> memory)
{
    var span = memory.Span;
    var reversed = new T[span.Length];

    for (var i = 0; i < span.Length; i++)
    {
        reversed[span.Length - i - 1] = span[i];
    }

    return new Memory<T>(reversed);
}

var qc = QuickChecker.CreateEmpty();

// Create and add a generator for `Memory<int>`
var generator = new ArbitraryMemoryGenerator<int>(
    ArbitraryInt32Generator.Default,
    Random.Shared,
    maximumSize: 32);

qc.AddGenerator(generator);

// Test function and validate that for all Memory<int> (Reverse(Reverse(memory)) == memory)
var result = qc.Run(
    target: static (Memory<int> memory) => Reverse(Reverse(memory)).Span.SequenceEqual(memory.Span),
    validate: static (result) => result);

Console.WriteLine(result.Type); // Success
```

## Usage

The main entry point for interacting with the library is the `QuickChecker` class.

To construct an instance with the default generators pre-registered, use an overload of the `CreateDefault` method.

```csharp
using QuickCheck;

// Using the default options
var qc = QuickChecker.CreateDefault();

// Using custom options
var configuredQc = QuickChecker.CreateDefault(options =>
{
    options.RunCount = 1000;
    options.Random = new Random(42);
});
```

To construct an instance with no pre-registered generators, use one of the `CreateEmpty` overloads.

```csharp
using QuickCheck;

// Using the default options
var qc = QuickChecker.CreateEmpty();

// Using custom options
var configuredQc = QuickChecker.CreateEmpty(options =>
{
    options.RunCount = 1000;
    options.Random = new Random(42);
});
```

### Generators

To test your functions, the `QuickChecker` must be able to generate values for every argument type of the target function.

This library provides a number of built-in generators for common types, including many numeric types, `char`, and `string`. These are added when using `CreateDefault` overloads.

Generic generators are available for `List<T>`, `Memory<T>`, and tuples, when given a generator for `T`. These must be added manually.

To add a generator to the `QuickChecker` instance, use the `AddGenerator` method:

```csharp
using QuickCheck.Generators;

var qc = QuickChecker.CreateEmpty();
qc.AddGenerator(ArbitraryInt32Generator.Default);
```

### Testing a Function

With the `QuickChecker` configured, you can test a function by calling the `Run` method.

Say I wrote a method to divide two integers, but return 0 if the divisor is 0. However, I forgot to handle the case where the divisor is 0.

```csharp
using QuickCheck;

// If b == 0, return 0, else return a / b. Oops!
static int NotSoSafeDivide(int a, int b) => a / b;

var qc = QuickChecker.CreateDefault();

var result = qc.Run(static (int a, int b) => NotSoSafeDivide(a, b));

Console.WriteLine(result.IsError); // True
Console.WriteLine(result.Exception is DivideByZeroException); // True
```

## Resources

The original QuickCheck library in Haskell:

- [Haskell's QuickCheck](https://hackage.haskell.org/package/QuickCheck)

I learned about QuickCheck from Jon Gjengset's great video on the Rust implementation:

- [Jon Gjengset YouTube video on QuickCheck in Rust](https://www.youtube.com/watch?v=64t-gPC33cc)
- [QuickCheck in Rust](https://github.com/BurntSushi/quickcheck)
