using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace QuickCheck.Xunit;

/// <summary>
/// A validated <see cref="PropertyAttribute"/> method together with the
/// generator for its argument list, ready to be turned into an
/// <see cref="AsyncProperty{T}"/> over an instance of the test class.
/// </summary>
internal sealed class PropertyMethod
{
    private readonly MethodInfo _method;

    private PropertyMethod(MethodInfo method, Generator<PropertyArguments> arguments)
    {
        _method = method;
        Arguments = arguments;
    }

    public Generator<PropertyArguments> Arguments { get; }

    /// <summary>
    /// Validates the method's shape and resolves a generator for every
    /// parameter.
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
            return new PropertyMethod(method, ArgumentsGenerator(method, generators));
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

    public AsyncProperty<PropertyArguments> ToProperty(object? testClassInstance) =>
        Property.ForAll(Arguments, arguments => InvokeAsync(testClassInstance, arguments.Values));

    private static Generator<PropertyArguments> ArgumentsGenerator(MethodInfo method, Type? generators)
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

        var parameters = method.GetParameters();
        var names = new string[parameters.Length];
        var argumentGenerators = new Generator<object?>[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];

            if (parameter.ParameterType.IsByRef)
            {
                throw new PropertyDefinitionException(
                    $"parameter '{parameter.Name}' is passed by reference (ref, in, or out), which is not supported.");
            }

            names[i] = parameter.Name!;
            argumentGenerators[i] = GeneratorResolver.ForParameter(parameter, generators, method.DeclaringType!);
        }

        return GeneratorReflection.Sequence(argumentGenerators)
            .Select(values => new PropertyArguments(names, values));
    }

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
