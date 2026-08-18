namespace QuickCheck;

/// <summary>
/// Represents an example that falsified a property.
/// </summary>
/// <typeparam name="T">The type of the falsifying value.</typeparam>
public sealed class Counterexample<T>
{
    internal Counterexample(T value, Exception? exception)
    {
        Value = value;
        Exception = exception;
    }

    /// <summary>Gets the value that falsified the property.</summary>
    public T Value { get; }

    /// <summary>
    /// Gets the exception the property threw for <see cref="Value"/>, or <see langword="null"/> if the
    /// property returned <see langword="false"/> instead of throwing.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>Returns a string representation of <see cref="Value"/>.</summary>
    /// <returns>The formatted falsifying value.</returns>
    public override string ToString() => ValueFormatter.Format(Value);
}
