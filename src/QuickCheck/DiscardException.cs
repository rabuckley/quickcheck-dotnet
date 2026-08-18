namespace QuickCheck;

/// <summary>
/// The exception that is thrown when the current example is discarded, because an assumption did not
/// hold or a value could not be generated.
/// </summary>
/// <remarks>
/// A property body must let this exception propagate; a <see langword="catch"/> clause that swallows
/// it turns a discarded example into a spuriously passing one.
/// </remarks>
public sealed class DiscardException : Exception
{
    internal DiscardException(string message) : base(message)
    {
    }
}
