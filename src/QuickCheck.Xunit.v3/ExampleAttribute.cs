namespace QuickCheck.Xunit;

/// <summary>
/// Pins one argument list that a <see cref="PropertyAttribute"/> method is always checked on,
/// whatever its generators produce.
/// </summary>
/// <remarks>
/// <para>
/// Explicit examples are checked before anything is generated, and the first one to fail ends the
/// check. They are checked on top of <see cref="PropertyAttribute.RunCount"/> rather than out of
/// it, and one the body discards with <see cref="Property.Assume"/> is skipped and reported in the
/// test output. A failing one is reported as it was written, unshrunk, which is the point: a
/// <see cref="PropertyAttribute.Replay"/> token names an example by its position in a random
/// stream, so it drifts to a different input as soon as a generator changes shape, where a pinned
/// argument list keeps testing the input the failure was found on.
/// </para>
/// <para>
/// Each value must be assignable to its parameter, or convertible to it by
/// <see cref="Convert.ChangeType(object?, Type)"/>; an enum parameter takes its underlying integral
/// value. A wrong count, an unconvertible value, or <see cref="PropertyAttribute.Replay"/> on the
/// same method is reported at discovery, as any other malformed property is. Attribute arguments
/// have to be compile-time constants, so a <see cref="decimal"/>, <see cref="DateTime"/> or
/// <see cref="Guid"/> parameter cannot be pinned here; use
/// <see cref="Property{T}.Example"/> in a <c>[Fact]</c> for those.
/// </para>
/// <para>
/// Reflection does not order attributes, so the pins on a method are checked in a canonical order
/// derived from their values rather than the order they are written: a method whose pins fail
/// reports the same one on every machine and runtime. <see cref="Property{T}.Example"/> keeps its
/// add order instead, which is an order the caller gave.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Property]
/// [Example(0, 0)]
/// public void Division_is_total(int a, int b) => _ = a / b;
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ExampleAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExampleAttribute"/> class.
    /// </summary>
    /// <param name="values">
    /// One value for each parameter of the property method, in order. A single
    /// <see langword="null"/> binds to the array rather than to its one element, so it is read as a
    /// one-value list holding <see langword="null"/>.
    /// </param>
    public ExampleAttribute(params object?[]? values) => Values = values ?? [null];

    /// <summary>
    /// Gets the values to call the property method with, one for each parameter.
    /// </summary>
    public IReadOnlyList<object?> Values { get; }
}
