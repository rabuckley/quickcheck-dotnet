using System.Reflection;
using System.Runtime.CompilerServices;

namespace QuickCheck.Xunit;

/// <summary>
/// Finds or derives a generator for each parameter of a property method.
/// </summary>
/// <remarks>
/// A parameter's generator comes from the first of: its
/// <see cref="GeneratorAttribute"/>; a public static <c>Generator&lt;T&gt;</c>
/// member of the <see cref="PropertyAttribute.Generators"/> type; the type's
/// <see cref="IArbitrary{TSelf}"/>; a built-in generator; or, for a type with
/// one public constructor, a generator derived from its parameters by the same
/// rules. Nullable annotations add <see langword="null"/> examples on top of
/// whatever the type resolves to, unless the generator was named explicitly.
/// Recursion through a type is unrolled to <see cref="MaxRecursionDepth"/>
/// levels, ending at <see langword="null"/> or an empty collection. A name may
/// resolve to a non-public member; matching by type considers only public ones,
/// so a registry can compose its entries from private helpers.
/// </remarks>
internal sealed class GeneratorResolver
{
    internal const int MaxRecursionDepth = 4;

    // A member asked for by name may be private: naming a helper on your own test
    // class is normal. A member found by type may not, or a registry could not keep
    // private helpers without making every parameter of that type ambiguous.
    private const BindingFlags MembersByName =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    private const BindingFlags MembersByType =
        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;

    private static readonly HashSet<Type> ListTypes =
    [
        typeof(List<>), typeof(IList<>), typeof(ICollection<>), typeof(IEnumerable<>),
        typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>)
    ];

    private readonly Type? _registry;
    private readonly Type _testClass;
    private readonly NullabilityInfoContext _nullability = new();
    private readonly List<Type> _ancestors = [];

    private GeneratorResolver(Type? registry, Type testClass)
    {
        _registry = registry;
        _testClass = testClass;
    }

    /// <summary>
    /// The generator for <paramref name="parameter"/> of a property method on
    /// <paramref name="testClass"/>, boxed to <see cref="object"/> so the
    /// arguments of a method can be sequenced together.
    /// </summary>
    /// <exception cref="PropertyDefinitionException">No generator can be found or derived.</exception>
    /// <param name="parameter">The parameter to generate.</param>
    /// <param name="generators">The <see cref="PropertyAttribute.Generators"/> type, if any.</param>
    /// <param name="testClass">The class declaring the property method.</param>
    public static Generator<object?> ForParameter(ParameterInfo parameter, Type? generators, Type testClass)
    {
        var resolver = new GeneratorResolver(generators, testClass);
        var type = parameter.ParameterType;

        try
        {
            return GeneratorReflection.Box(type, resolver.ForParameterCore(parameter));
        }
        catch (PropertyDefinitionException exception)
        {
            throw new PropertyDefinitionException(
                $"Parameter '{parameter.Name}' ({TypeName(type)}): {exception.Message}", exception);
        }
    }

    /// <summary>
    /// The generator for one parameter, whether it is a parameter of the
    /// property method or of a constructor a generator is being derived from.
    /// A named generator is taken as given: it short-circuits both the
    /// nullability annotations and the recursion guard.
    /// </summary>
    private object ForParameterCore(ParameterInfo parameter) =>
        parameter.GetCustomAttribute<GeneratorAttribute>() is { } named
            ? Named(named, parameter.ParameterType)
            : ResolveTyped(parameter.ParameterType, _nullability.Create(parameter));

    private object Named(GeneratorAttribute attribute, Type type)
    {
        Type[] sources = attribute.Source is { } source
            ? [source]
            : _registry is { } registry && registry != _testClass ? [_testClass, registry] : [_testClass];

        var matches = sources
            .SelectMany(s => s.GetMember(attribute.MemberName, MembersByName).Select(member => (Source: s, Member: member)))
            .Where(static match => IsGeneratorMember(match.Member))
            .ToList();

        var sourceNames = string.Join(" or ", sources.Select(TypeName));

        return matches switch
        {
            [] => throw new PropertyDefinitionException(
                $"no static generator member named '{attribute.MemberName}' was found on {sourceNames}."),
            [var match] => CheckedMemberGenerator(match.Member, type),
            _ => throw new PropertyDefinitionException(
                $"the generator name '{attribute.MemberName}' is ambiguous: it matches members on {sourceNames}.")
        };
    }

    private object CheckedMemberGenerator(MemberInfo member, Type type)
    {
        var expected = GeneratorReflection.GeneratorTypeFor(type);
        var actual = GeneratorMemberType(member);

        if (!expected.IsAssignableFrom(actual))
        {
            throw new PropertyDefinitionException(
                $"'{MemberName(member)}' is a {TypeName(actual)}, not a {TypeName(expected)}.");
        }

        return MemberGenerator(member);
    }

    private object ResolveTyped(Type type, NullabilityInfo? nullability)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            if (Registered(type) is { } registered)
            {
                return registered;
            }

            return Depth(underlying) >= MaxRecursionDepth
                ? GeneratorReflection.NullStruct(underlying)
                : GeneratorReflection.Nullable(underlying, ResolveNonNull(underlying, nullability?.GenericTypeArguments.FirstOrDefault()));
        }

        if (!type.IsValueType && nullability?.ReadState == NullabilityState.Nullable)
        {
            return Depth(type) >= MaxRecursionDepth
                ? GeneratorReflection.NullClass(type)
                : GeneratorReflection.OrNull(type, ResolveNonNull(type, nullability));
        }

        return ResolveNonNull(type, nullability);
    }

    private object ResolveNonNull(Type type, NullabilityInfo? nullability)
    {
        if (Registered(type) is { } registered)
        {
            return registered;
        }

        if (Arbitrary(type) is { } arbitrary)
        {
            return arbitrary;
        }

        if (BuiltIn(type) is { } builtIn)
        {
            return builtIn;
        }

        if (type.IsArray && type.GetArrayRank() == 1)
        {
            var item = type.GetElementType()!;

            return Depth(item) >= MaxRecursionDepth
                ? GeneratorReflection.EmptyArray(item)
                : GeneratorReflection.Array(item, ResolveTyped(item, nullability?.ElementType));
        }

        if (type.IsGenericType && ListTypes.Contains(type.GetGenericTypeDefinition()))
        {
            var item = type.GetGenericArguments()[0];

            return Depth(item) >= MaxRecursionDepth
                ? GeneratorReflection.EmptyList(item, type)
                : GeneratorReflection.List(item, type, ResolveTyped(item, nullability?.GenericTypeArguments.FirstOrDefault()));
        }

        return Construct(type);
    }

    private object? Registered(Type type)
    {
        if (_registry is null)
        {
            return null;
        }

        var expected = GeneratorReflection.GeneratorTypeFor(type);
        var matches = _registry.GetMembers(MembersByType)
            .Where(member => IsGeneratorMember(member) && expected.IsAssignableFrom(GeneratorMemberType(member)))
            .ToList();

        return matches switch
        {
            [] => null,
            [var member] => MemberGenerator(member),
            _ => throw new PropertyDefinitionException(
                $"{TypeName(_registry)} has more than one {TypeName(expected)} member "
                + $"({string.Join(", ", matches.Select(static m => m.Name))}); name one with [Generator].")
        };
    }

    private static object? Arbitrary(Type type)
    {
        var declaresArbitrary = type.GetInterfaces().Any(candidate =>
            candidate.IsGenericType
            && candidate.GetGenericTypeDefinition() == typeof(IArbitrary<>)
            && candidate.GetGenericArguments()[0] == type);

        return declaresArbitrary ? GeneratorReflection.Arbitrary(type) : null;
    }

    private static object? BuiltIn(Type type)
    {
        if (type == typeof(bool))
        {
            return Generate.Boolean();
        }

        if (type == typeof(char))
        {
            return Generate.Char();
        }

        if (type == typeof(string))
        {
            return Generate.String();
        }

        if (type.IsEnum)
        {
            return GeneratorReflection.Enum(type);
        }

        if (type == typeof(DateTime))
        {
            return Generate.DateTime();
        }

        if (type == typeof(DateTimeOffset))
        {
            return Generate.DateTimeOffset();
        }

        if (type == typeof(DateOnly))
        {
            return Generate.DateOnly();
        }

        if (type == typeof(TimeOnly))
        {
            return Generate.TimeOnly();
        }

        if (type == typeof(TimeSpan))
        {
            return Generate.TimeSpan();
        }

        if (type == typeof(Guid))
        {
            return Generate.Guid();
        }

        if (type == typeof(sbyte) || type == typeof(byte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
            || type == typeof(nint) || type == typeof(nuint))
        {
            return GeneratorReflection.Integer(type);
        }

        return null;
    }

    private object Construct(Type type)
    {
        if (type.IsAbstract || type.IsPointer || type.IsByRef || type.IsByRefLike || type.ContainsGenericParameters
            || typeof(Delegate).IsAssignableFrom(type) || type == typeof(object))
        {
            throw new PropertyDefinitionException($"no generator can be derived for {TypeName(type)}.");
        }

        var isFrameworkType = type.Namespace is "System" || type.Namespace?.StartsWith("System.", StringComparison.Ordinal) == true;

        if (isFrameworkType && !typeof(ITuple).IsAssignableFrom(type)
            && !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)))
        {
            throw new PropertyDefinitionException(
                $"QuickCheck has no built-in generator for {TypeName(type)}; "
                + "supply one with [Generator] or a public static Generator<T> member on the Generators type.");
        }

        if (Depth(type) >= MaxRecursionDepth)
        {
            throw new PropertyDefinitionException(
                $"{TypeName(type)} is recursive without a nullable or collection member to end the recursion.");
        }

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (constructors.Length != 1)
        {
            throw new PropertyDefinitionException(constructors.Length == 0
                ? $"{TypeName(type)} has no public constructor to derive a generator from."
                : $"{TypeName(type)} has {constructors.Length} public constructors; a derived generator needs exactly one.");
        }

        var constructor = constructors[0];
        _ancestors.Add(type);

        try
        {
            var arguments = constructor.GetParameters()
                .Select(parameter =>
                {
                    try
                    {
                        return GeneratorReflection.Box(parameter.ParameterType, ForParameterCore(parameter));
                    }
                    catch (PropertyDefinitionException exception)
                    {
                        throw new PropertyDefinitionException(
                            $"in {TypeName(type)}, member '{parameter.Name}' ({TypeName(parameter.ParameterType)}): {exception.Message}");
                    }
                })
                .ToArray();

            return GeneratorReflection.Construct(type, constructor, arguments);
        }
        finally
        {
            _ancestors.RemoveAt(_ancestors.Count - 1);
        }
    }

    private int Depth(Type type) => _ancestors.Count(ancestor => ancestor == type);

    private static bool IsGeneratorMember(MemberInfo member)
    {
        // A static auto-property's backing field, or a primary constructor's capture
        // field, has the same Generator<T> type as the member the author wrote:
        // counting both would make every such registry entry ambiguous.
        if (member.IsDefined(typeof(CompilerGeneratedAttribute)))
        {
            return false;
        }

        return member switch
        {
            PropertyInfo property => property.GetIndexParameters().Length == 0 && IsGeneratorType(property.PropertyType),
            FieldInfo field => IsGeneratorType(field.FieldType),
            MethodInfo method => method.GetParameters().Length == 0 && !method.IsGenericMethodDefinition
                                 && !method.IsSpecialName && IsGeneratorType(method.ReturnType),
            _ => false
        };
    }

    private static bool IsGeneratorType(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Generator<>))
            {
                return true;
            }
        }

        return false;
    }

    private static Type GeneratorMemberType(MemberInfo member) => member switch
    {
        PropertyInfo property => property.PropertyType,
        FieldInfo field => field.FieldType,
        MethodInfo method => method.ReturnType,
        _ => throw new System.Diagnostics.UnreachableException()
    };

    private static object MemberGenerator(MemberInfo member)
    {
        object? generator;

        try
        {
            generator = member switch
            {
                PropertyInfo property => property.GetValue(null),
                FieldInfo field => field.GetValue(null),
                MethodInfo method => method.Invoke(null, null),
                _ => throw new System.Diagnostics.UnreachableException()
            };
        }
        catch (Exception exception) when (exception is TargetInvocationException or TypeInitializationException)
        {
            var cause = Unwrap(exception);

            throw new PropertyDefinitionException(
                $"'{MemberName(member)}' threw {cause.GetType().Name}: {cause.Message}", cause);
        }

        return generator ?? throw new PropertyDefinitionException($"'{MemberName(member)}' returned null.");
    }

    /// <summary>
    /// What a member actually threw. Reflection reports it wrapped in a
    /// <see cref="TargetInvocationException"/>, and a static initializer's failure in a
    /// <see cref="TypeInitializationException"/>, neither of which says anything the member's own
    /// name does not.
    /// </summary>
    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException or TypeInitializationException && exception.InnerException is { } inner
            ? Unwrap(inner)
            : exception;

    private static string MemberName(MemberInfo member) => $"{TypeName(member.DeclaringType!)}.{member.Name}";

    internal static string TypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying)
        {
            return TypeName(underlying) + "?";
        }

        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[]";
        }

        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name[..type.Name.IndexOf('`')];
        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }
}
