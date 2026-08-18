namespace QuickCheck.Xunit;

/// <summary>
/// Names the generator for one parameter of a <see cref="PropertyAttribute"/>
/// method, or of the constructor of a type a generator is derived for (a
/// record's positional parameter included): a static <c>Generator&lt;T&gt;</c>
/// property, field, or parameterless method, looked up on <see cref="Source"/>
/// if given, otherwise on the test class and the property's
/// <see cref="PropertyAttribute.Generators"/> type. The member may be
/// private, unlike one found by type. Use <see langword="nameof"/> for the
/// name.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class GeneratorAttribute : Attribute
{
    /// <summary>
    /// Names a generator member on the test class or the property's
    /// <see cref="PropertyAttribute.Generators"/> type.
    /// </summary>
    /// <param name="memberName">The static member's name.</param>
    public GeneratorAttribute(string memberName)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);
        MemberName = memberName;
    }

    /// <summary>
    /// Names a generator member on <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The type declaring the member.</param>
    /// <param name="memberName">The static member's name.</param>
    public GeneratorAttribute(Type source, string memberName)
        : this(memberName)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
    }

    /// <summary>The static member's name.</summary>
    public string MemberName { get; }

    /// <summary>The type declaring the member, when given explicitly.</summary>
    public Type? Source { get; }
}
