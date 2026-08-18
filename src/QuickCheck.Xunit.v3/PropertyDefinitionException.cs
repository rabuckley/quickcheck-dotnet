namespace QuickCheck.Xunit;

/// <summary>
/// A <see cref="PropertyAttribute"/> method cannot be run as written: its
/// shape is unsupported, its settings are invalid, or a parameter has no
/// generator. The message names the problem; one raised while validating the
/// method names the method too.
/// </summary>
internal sealed class PropertyDefinitionException : Exception
{
    public PropertyDefinitionException(string message)
        : base(message)
    {
    }

    public PropertyDefinitionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
