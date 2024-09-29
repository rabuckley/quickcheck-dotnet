using System.Diagnostics.CodeAnalysis;

namespace QuickCheck;

/// <summary>
/// Thrown when a <see cref="QuickChecker"/> is missing a required <see cref="IArbitraryValueGenerator{T}"/>.
/// </summary>
public sealed class MissingGeneratorException : Exception
{
    /// <summary>
    /// The type for which an <see cref="IArbitraryValueGenerator{T}"/> is missing.
    /// </summary>
    public Type Type { get; }

    public MissingGeneratorException(string message, Type type) : base(message)
    {
        Type = type;
    }

    [DoesNotReturn]
    public static void Throw(Type type)
    {
        throw new MissingGeneratorException(
            $"No generator is registered for type '{type}'. Add one to your {nameof(QuickChecker)} before running.",
            type);
    }
}