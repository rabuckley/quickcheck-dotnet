namespace QuickCheck;

/// <summary>
/// Declares the default generator for a type, so that a test-framework adapter deriving generators
/// from parameter types can produce it without being told how.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IArbitrary<TSelf> where TSelf : IArbitrary<TSelf>
{
    /// <summary>Gets the default generator for <typeparamref name="TSelf"/>.</summary>
    static abstract Generator<TSelf> Arbitrary { get; }
}
