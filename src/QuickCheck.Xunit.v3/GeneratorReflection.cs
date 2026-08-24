using System.Numerics;
using System.Reflection;

namespace QuickCheck.Xunit;

/// <summary>
/// The generator plumbing the resolver needs to work from reflected parameter types: generic
/// <see cref="Generate"/> operations invoked with a runtime <see cref="Type"/> argument, each
/// public method mirroring a private generic implementation below it, plus
/// <see cref="Sequence"/> over the already-boxed generators of a method's arguments.
/// </summary>
internal static class GeneratorReflection
{
    private static readonly Type GenericGeneratorType = typeof(Generator<>);

    public static Type GeneratorTypeFor(Type valueType) => GenericGeneratorType.MakeGenericType(valueType);

    public static Generator<object?> Box(Type valueType, object generator) => (Generator<object?>)Invoke(nameof(BoxOf), valueType, generator);
    public static object Arbitrary(Type type) => Invoke(nameof(ArbitraryOf), type);
    public static object Integer(Type type) => Invoke(nameof(IntegerOf), type);
    public static object FloatingPoint(Type type) => Invoke(nameof(FloatingPointOf), type);
    public static object Enum(Type type) => Invoke(nameof(EnumOf), type);
    public static object Nullable(Type underlying, object generator) => Invoke(nameof(NullableOf), underlying, generator);
    public static object OrNull(Type type, object generator) => Invoke(nameof(OrNullOf), type, generator);
    public static object NullStruct(Type underlying) => Invoke(nameof(NullStructOf), underlying);
    public static object NullClass(Type type) => Invoke(nameof(NullClassOf), type);
    public static object Array(Type item, object generator) => Invoke(nameof(ArrayOf), item, generator);
    public static object EmptyArray(Type item) => Invoke(nameof(EmptyArrayOf), item);

    /// <summary>
    /// A generator of <see cref="List{T}"/> typed as <paramref name="collection"/>,
    /// which must be <see cref="List{T}"/> or an interface it implements.
    /// </summary>
    public static object List(Type item, Type collection, object generator) =>
        Invoke(nameof(ListOf), [item, collection], generator);

    /// <inheritdoc cref="List"/>
    public static object EmptyList(Type item, Type collection) =>
        Invoke(nameof(EmptyListOf), [item, collection]);

    /// <summary>
    /// A generator of <see cref="HashSet{T}"/> typed as <paramref name="collection"/>,
    /// which must be <see cref="HashSet{T}"/> or an interface it implements.
    /// </summary>
    public static object Set(Type item, Type collection, object generator) =>
        Invoke(nameof(SetOf), [item, collection], generator);

    /// <inheritdoc cref="Set"/>
    public static object EmptySet(Type item, Type collection) =>
        Invoke(nameof(EmptySetOf), [item, collection]);

    /// <summary>
    /// A generator of <see cref="Dictionary{TKey,TValue}"/> typed as
    /// <paramref name="collection"/>, which must be <see cref="Dictionary{TKey,TValue}"/> or an
    /// interface it implements. The <c>notnull</c> key constraint binds only at compile time, so
    /// any runtime key type is accepted here.
    /// </summary>
    public static object Dictionary(Type key, Type value, Type collection, object keys, object values) =>
        Invoke(nameof(DictionaryOf), [key, value, collection], keys, values);

    /// <inheritdoc cref="Dictionary"/>
    public static object EmptyDictionary(Type key, Type value, Type collection) =>
        Invoke(nameof(EmptyDictionaryOf), [key, value, collection]);

    public static object Construct(Type type, ConstructorInfo constructor, Generator<object?>[] arguments) =>
        Invoke(nameof(ConstructOf), type, constructor, arguments);

    /// <summary>
    /// A generator that draws from each of <paramref name="generators"/> in turn, so that the
    /// shrinker sees one span per element and can shrink them independently.
    /// </summary>
    public static Generator<object?[]> Sequence(Generator<object?>[] generators) =>
        Generate.From(source =>
        {
            var values = new object?[generators.Length];

            for (var i = 0; i < generators.Length; i++)
            {
                values[i] = source.Draw(generators[i]);
            }

            return values;
        });

    private static object Invoke(string name, Type typeArgument, params object?[] arguments) =>
        Invoke(name, [typeArgument], arguments);

    private static object Invoke(string name, Type[] typeArguments, params object?[] arguments)
    {
        var method = typeof(GeneratorReflection).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

        try
        {
            return method.MakeGenericMethod(typeArguments).Invoke(null, arguments)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(exception.InnerException);
            throw;
        }
    }

    private static Generator<object?> BoxOf<T>(Generator<T> generator) => generator.Select(static value => (object?)value);

    /// <summary>
    /// The type's own <see cref="IArbitrary{TSelf}.Arbitrary"/>. Reaching it through a constrained
    /// call keeps classes, structs, and interfaces that reimplement the static member on one path;
    /// the interface map that would find it on a class does not exist for an interface.
    /// </summary>
    private static Generator<T> ArbitraryOf<T>() where T : IArbitrary<T> =>
        T.Arbitrary ?? throw new PropertyDefinitionException(
            $"{GeneratorResolver.TypeName(typeof(T))}.Arbitrary returned null.");

    private static Generator<T> IntegerOf<T>() where T : IBinaryInteger<T>, IMinMaxValue<T> => Generate.Integer<T>();

    private static Generator<T> FloatingPointOf<T>() where T : IFloatingPointIeee754<T>, IMinMaxValue<T> => Generate.FloatingPoint<T>();

    private static Generator<T> EnumOf<T>() where T : struct, Enum => Generate.Enum<T>();

    private static Generator<T?> NullableOf<T>(Generator<T> generator) where T : struct => generator.Nullable();

    private static Generator<T?> OrNullOf<T>(Generator<T> generator) where T : class => generator.OrNull();

    private static Generator<T?> NullStructOf<T>() where T : struct => Generate.Constant<T?>(null);

    private static Generator<T?> NullClassOf<T>() where T : class => Generate.Constant<T?>(null);

    private static Generator<TCollection> ListOf<T, TCollection>(Generator<T> item) where TCollection : class =>
        item.List().Select(static list => (TCollection)(object)list);

    private static Generator<T[]> ArrayOf<T>(Generator<T> item) => item.Array();

    // A fresh list per example: callers may mutate what they are given.
    private static Generator<TCollection> EmptyListOf<T, TCollection>() where TCollection : class =>
        Generate.From(static _ => (TCollection)(object)new List<T>());

    private static Generator<TCollection> SetOf<T, TCollection>(Generator<T> item) where TCollection : class =>
        item.HashSet().Select(static set => (TCollection)(object)set);

    private static Generator<TCollection> EmptySetOf<T, TCollection>() where TCollection : class =>
        Generate.From(static _ => (TCollection)(object)new HashSet<T>());

    private static Generator<TCollection> DictionaryOf<TKey, TValue, TCollection>(
        Generator<TKey> keys,
        Generator<TValue> values)
        where TKey : notnull
        where TCollection : class =>
        Generate.Dictionary(keys, values).Select(static dictionary => (TCollection)(object)dictionary);

    private static Generator<TCollection> EmptyDictionaryOf<TKey, TValue, TCollection>()
        where TKey : notnull
        where TCollection : class =>
        Generate.From(static _ => (TCollection)(object)new Dictionary<TKey, TValue>());

    private static Generator<T[]> EmptyArrayOf<T>() => Generate.Constant(System.Array.Empty<T>());

    private static Generator<T> ConstructOf<T>(ConstructorInfo constructor, Generator<object?>[] arguments) =>
        Sequence(arguments).Select(values =>
        {
            try
            {
                return (T)constructor.Invoke(values);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                throw Rejected<T>(constructor, values, exception.InnerException);
            }
        });

    /// <summary>
    /// A guard clause in the constructor turned down what the derived generator
    /// produced, which is a fact about the definition rather than a property
    /// failure: name the type and show the arguments, or the author sees only
    /// the guard's own message with nothing to tie it back to QuickCheck.
    /// </summary>
    private static PropertyDefinitionException Rejected<T>(ConstructorInfo constructor, object?[] values, Exception inner)
    {
        var name = GeneratorResolver.TypeName(typeof(T));
        var rejected = new PropertyArguments([.. constructor.GetParameters().Select(static parameter => parameter.Name!)], values);

        return new PropertyDefinitionException(
            $"Deriving a generator for {name}: its constructor rejected the generated arguments ({rejected}). "
            + $"Supply a generator with [Generator] or a public static Generator<{name}> member on the Generators type.",
            inner);
    }
}
