using System.Runtime.CompilerServices;
using Xunit;
using Xunit.v3;

namespace QuickCheck.Xunit;

/// <summary>
/// Marks a test method as a property: xUnit generates each parameter from a
/// <see cref="Generator{T}"/>, runs the method on many examples, and on failure
/// reports the smallest example it can shrink to.
/// </summary>
/// <remarks>
/// <para>
/// Each parameter's generator is found, in order, from: a
/// <see cref="GeneratorAttribute"/> on the parameter; a public static
/// <c>Generator&lt;T&gt;</c> member of the <see cref="Generators"/> type; the
/// parameter type's <see cref="IArbitrary{TSelf}"/> implementation; or a
/// built-in default for primitives, strings, enums, nullables, arrays and
/// lists, tuples, and types with a single public constructor (records
/// included), applied recursively.
/// </para>
/// <para>
/// The method may return <see langword="void"/>, <see cref="bool"/>,
/// <see cref="Task"/>, <see cref="ValueTask"/>, or their <see cref="bool"/>
/// forms; a <see langword="false"/> return falsifies the property just as an
/// exception does. Call <see cref="Property.Assume"/> to discard an example.
/// </para>
/// </remarks>
[XunitTestCaseDiscoverer(typeof(PropertyDiscoverer))]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class PropertyAttribute(
    [CallerFilePath] string? sourceFilePath = null,
    [CallerLineNumber] int sourceLineNumber = -1)
    : FactAttribute(sourceFilePath, sourceLineNumber)
{
    /// <summary>
    /// The number of examples to test; see <see cref="CheckOptions.RunCount"/>.
    /// Zero (the default) uses the library default.
    /// </summary>
    public int RunCount { get; set; }

    /// <summary>
    /// The seed for example generation; see <see cref="CheckOptions.Seed"/>.
    /// When not set, a fresh seed is chosen for each run and reported in the
    /// test output.
    /// </summary>
    public ulong Seed
    {
        get;
        set
        {
            field = value;
            HasSeed = true;
        }
    }

    internal bool HasSeed { get; private set; }

    /// <summary>
    /// A replay token (<c>seed:run</c>) from an earlier failure report; see
    /// <see cref="CheckOptions.Replay"/>. Checks only that example.
    /// </summary>
    public string? Replay { get; set; }

    /// <summary>
    /// The shrinking budget; see <see cref="CheckOptions.MaxShrinkAttempts"/>.
    /// Negative (the default) uses the library default; zero disables shrinking.
    /// </summary>
    public int MaxShrinkAttempts { get; set; } = -1;

    /// <summary>
    /// A type whose public static <c>Generator&lt;T&gt;</c> properties, fields, or
    /// parameterless methods supply the generator for any parameter (or
    /// nested member) of type <c>T</c>. Also searched, private members
    /// included, by <see cref="GeneratorAttribute"/> names, so a registry can
    /// compose its entries from private helpers.
    /// </summary>
    public Type? Generators { get; set; }

    /// <summary>
    /// The <see cref="CheckOptions"/> these settings describe.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A setting is out of range.</exception>
    /// <exception cref="FormatException"><see cref="Replay"/> is not a valid token.</exception>
    internal CheckOptions ToCheckOptions()
    {
        var options = CheckOptions.Default;

        if (RunCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(RunCount), RunCount, "RunCount must be positive, or zero for the default.");
        }

        if (RunCount != 0)
        {
            options = options with { RunCount = RunCount };
        }

        if (HasSeed)
        {
            options = options with { Seed = Seed };
        }

        if (Replay is not null)
        {
            options = options with { Replay = QuickCheck.Replay.Parse(Replay) };
        }

        if (MaxShrinkAttempts >= 0)
        {
            options = options with { MaxShrinkAttempts = MaxShrinkAttempts };
        }

        return options;
    }
}
