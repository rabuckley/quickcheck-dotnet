using System.Diagnostics.CodeAnalysis;

namespace QuickCheck.Exceptions;

/// <summary>
/// Thrown when a <see cref="QuickChecker"/> is missing a required <see cref="IArbitraryValueGenerator{T}"/>.
/// </summary>
public sealed class MissingGeneratorException : Exception
{
    /// <summary>
    /// The type for which an <see cref="IArbitraryValueGenerator{T}"/> is missing.
    /// </summary>
    public Type Type { get; }

    private MissingGeneratorException(string message, Type type) : base(message)
    {
        Type = type;
    }

    [DoesNotReturn]
    internal static void Throw(Type type)
    {
        throw new MissingGeneratorException(
            $"No generator is registered for type '{type.Name}'. Add one to your {nameof(QuickChecker)} before running.",
            type);
    }
}
