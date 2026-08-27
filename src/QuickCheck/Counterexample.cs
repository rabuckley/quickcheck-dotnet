namespace QuickCheck;

/// <summary>
/// Represents an example that falsified a property.
/// </summary>
/// <typeparam name="T">The type of the falsifying value.</typeparam>
public sealed class Counterexample<T>
{
    internal Counterexample(T value, Exception? exception, bool isExplicit)
    {
        Value = value;
        Exception = exception;
        IsExplicit = isExplicit;
    }

    /// <summary>Gets the value that falsified the property.</summary>
    public T Value { get; }

    /// <summary>
    /// Gets the exception the property threw for <see cref="Value"/>, or <see langword="null"/> if the
    /// property returned <see langword="false"/> instead of throwing.
    /// </summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets a value indicating whether <see cref="Value"/> was pinned with
    /// <see cref="Property{T}.Example"/> rather than generated. A pinned value is checked as it was
    /// given, so it was not shrunk and no <see cref="PropertyResult{T}.Replay"/> token reproduces it.
    /// </summary>
    public bool IsExplicit { get; }

    /// <summary>Returns a string representation of <see cref="Value"/>.</summary>
    /// <returns>The formatted falsifying value.</returns>
    public override string ToString() => ValueFormatter.Format(Value);
}
