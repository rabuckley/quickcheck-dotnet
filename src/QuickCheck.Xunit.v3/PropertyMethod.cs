using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace QuickCheck.Xunit;

/// <summary>
/// A validated <see cref="PropertyAttribute"/> method together with the
/// generator for its argument list and any <see cref="ExampleAttribute"/>
/// pins, ready to be turned into an <see cref="AsyncProperty{T}"/> over an
/// instance of the test class.
/// </summary>
/// <remarks>
/// Hidden from stack traces so that the frames between the test method and the core's body adapter
/// neither clutter the trace xUnit prints nor count as the frame a failure came through.
/// </remarks>
[StackTraceHidden]
internal sealed class PropertyMethod
{
    private readonly MethodInfo _method;
    private readonly PropertyArguments[] _examples;

    private PropertyMethod(
        MethodInfo method, Generator<PropertyArguments> arguments, PropertyArguments[] examples)
    {
        _method = method;
        _examples = examples;
        Arguments = arguments;
    }

    public Generator<PropertyArguments> Arguments { get; }

    /// <summary>
    /// Validates the method's shape, resolves a generator for every parameter,
    /// and reads and checks its <see cref="ExampleAttribute"/> pins.
    /// </summary>
    /// <param name="method">The property method.</param>
    /// <param name="generators">
    /// The <see cref="PropertyAttribute.Generators"/> type, if any.
    /// </param>
    /// <exception cref="PropertyDefinitionException">
    /// The method cannot be run as a property; the message says why.
    /// </exception>
    public static PropertyMethod Create(MethodInfo method, Type? generators)
    {
        try
        {
            var parameters = method.GetParameters();
            var names = Array.ConvertAll(parameters, static parameter => parameter.Name!);

            return new PropertyMethod(
                method,
                ArgumentsGenerator(method, parameters, names, generators),
                ExplicitExamples(method, parameters, names));
        }
        catch (PropertyDefinitionException exception)
        {
            throw new PropertyDefinitionException($"{Describe(method)}: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// How a property method is named at the head of every message about it,
    /// so a failure at discovery says which method it is about.
    /// </summary>
    public static string Describe(MethodInfo method) =>
        $"[Property] method {method.DeclaringType!.Name}.{method.Name}";

    public AsyncProperty<PropertyArguments> ToProperty(object? testClassInstance)
    {
        var property = Property.ForAll(Arguments, arguments => InvokeAsync(testClassInstance, arguments.Values));

        foreach (var example in _examples)
        {
            property = property.Example(example);
        }

        return property;
    }

    private static Generator<PropertyArguments> ArgumentsGenerator(
        MethodInfo method, ParameterInfo[] parameters, string[] names, Type? generators)
    {
        if (method.IsGenericMethodDefinition)
        {
            throw new PropertyDefinitionException("generic methods are not supported.");
        }

        if (method.ReturnType == typeof(void) && method.IsDefined(typeof(AsyncStateMachineAttribute)))
        {
            throw new PropertyDefinitionException("'async void' is not supported; return Task or ValueTask.");
        }

        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(bool)
            && method.ReturnType != typeof(Task) && method.ReturnType != typeof(Task<bool>)
            && method.ReturnType != typeof(ValueTask) && method.ReturnType != typeof(ValueTask<bool>))
        {
            throw new PropertyDefinitionException(
                $"the return type {GeneratorResolver.TypeName(method.ReturnType)} is not supported; "
                + "return void, bool, Task, ValueTask, Task<bool>, or ValueTask<bool>.");
        }

        var argumentGenerators = new Generator<object?>[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.ParameterType.IsByRef)
            {
                throw new PropertyDefinitionException(
                    $"parameter '{parameter.Name}' is passed by reference (ref, in, or out), which is not supported.");
            }

            argumentGenerators[i] = GeneratorResolver.ForParameter(parameter, generators, method.DeclaringType!);
        }

        return Generate.Sequence(argumentGenerators)
            .Select(values => new PropertyArguments(names, values));
    }

    /// <summary>
    /// Reads the method's <see cref="ExampleAttribute"/> pins, checks each
    /// against the parameter list, and orders them by the text a report would
    /// print them as.
    /// </summary>
    /// <remarks>
    /// Validating here rather than at the point of use means a malformed pin is
    /// reported at discovery, on the test's own node, like any other way of
    /// writing a property wrong. Ordering here is what makes a method whose
    /// pins fail report the same one everywhere: reflection does not order
    /// attributes, and only the first failure is reported, so without a key of
    /// the adapter's own the reported pin would vary by machine and runtime.
    /// </remarks>
    private static PropertyArguments[] ExplicitExamples(
        MethodInfo method, ParameterInfo[] parameters, string[] names)
    {
        var attributes = method.GetCustomAttributes<ExampleAttribute>().ToArray();
        var examples = new PropertyArguments[attributes.Length];

        for (var i = 0; i < attributes.Length; i++)
        {
            var values = attributes[i].Values;

            if (values.Count != parameters.Length)
            {
                throw new PropertyDefinitionException(
                    $"[Example] has {values.Count} value{Plural(values.Count)} but the method has "
                    + $"{parameters.Length} parameter{Plural(parameters.Length)}.");
            }

            var arguments = new object?[parameters.Length];

            for (var argument = 0; argument < parameters.Length; argument++)
            {
                arguments[argument] = Coerce(values[argument], parameters[argument]);
            }

            examples[i] = new PropertyArguments(names, arguments);
        }

        Array.Sort(examples, static (left, right) =>
            StringComparer.Ordinal.Compare(left.ToString(), right.ToString()));

        return examples;
    }

    /// <summary>
    /// The attribute value as the parameter's type. An attribute argument is
    /// boxed as whatever the C# literal was, so <c>[Example(3)]</c> arrives as
    /// an <see cref="int"/> even for a <see cref="byte"/> parameter, which is
    /// why this converts rather than only widening. Conversion still rejects
    /// what the narrower type cannot hold.
    /// </summary>
    private static object? Coerce(object? value, ParameterInfo parameter)
    {
        var type = parameter.ParameterType;
        var target = Nullable.GetUnderlyingType(type) ?? type;

        if (value is null)
        {
            return target != type || !type.IsValueType
                ? null
                : throw new PropertyDefinitionException(
                    $"{Describe(parameter)}: cannot use null; {GeneratorResolver.TypeName(type)} cannot hold it.");
        }

        if (target.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            return target.IsEnum
                ? Enum.ToObject(target, Convert.ChangeType(value, target.GetEnumUnderlyingType(), CultureInfo.InvariantCulture)!)
                : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is InvalidCastException or OverflowException or FormatException)
        {
            throw new PropertyDefinitionException(
                $"{Describe(parameter)}: cannot use {ValueFormatter.Format(value)}. {exception.Message}");
        }

        static string Describe(ParameterInfo parameter) =>
            $"[Example] parameter '{parameter.Name}' ({GeneratorResolver.TypeName(parameter.ParameterType)})";
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private async Task<bool> InvokeAsync(object? testClassInstance, object?[] arguments)
    {
        object? result;

        try
        {
            result = _method.Invoke(testClassInstance, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(exception.InnerException);
            throw;
        }

        switch (result)
        {
            case null:
                return true;
            case bool holds:
                return holds;

            // Task<bool> derives from Task, so only the declared return type can say whether the
            // value a method happens to hand back is meant as the property's verdict.
            case Task<bool> task when _method.ReturnType == typeof(Task<bool>):
                return await task.ConfigureAwait(false);
            case Task task:
                await task.ConfigureAwait(false);
                return true;
            case ValueTask<bool> valueTask:
                return await valueTask.ConfigureAwait(false);
            case ValueTask valueTask:
                await valueTask.ConfigureAwait(false);
                return true;
            default:
                throw new System.Diagnostics.UnreachableException("Return type was validated at construction.");
        }
    }
}
