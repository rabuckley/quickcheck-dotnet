using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace QuickCheck.Running;

/// <summary>
/// Identifies how an example failed: the exception type and the user frame it left through, or
/// neither when the property returned <see langword="false"/>.
/// </summary>
/// <param name="ExceptionType">
/// The type of exception thrown, or <see langword="null"/> when the property returned <see langword="false"/>.
/// </param>
/// <param name="Origin">
/// The frame in user code the exception left through, or <see langword="null"/> when there was no
/// exception or its trace holds no user frame.
/// </param>
internal readonly record struct FailureKey(Type? ExceptionType, FailureOrigin? Origin)
{
    public static FailureKey None => default;

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "A frame whose method metadata was trimmed is skipped, so the key degrades to the exception type alone rather than breaking.")]
    public static FailureKey For(Exception? exception)
    {
        if (exception is null)
        {
            return None;
        }

        FailureOrigin? origin = null;

        foreach (var frame in new StackTrace(exception, fNeedFileInfo: true).GetFrames())
        {
            if (frame.GetMethod() is not { } method)
            {
                continue;
            }

            if (IsLibrary(method))
            {
                break;
            }

            if (IsRuntime(method) || IsHidden(method))
            {
                continue;
            }

            origin = new FailureOrigin(method, frame.GetFileLineNumber());
        }

        return new FailureKey(exception.GetType(), origin);
    }

    private static bool IsLibrary(MethodBase method) => method.Module.Assembly == typeof(FailureKey).Assembly;

    private static bool IsRuntime(MethodBase method)
    {
        var assembly = method.Module.Assembly;
        return assembly == typeof(object).Assembly
            || assembly.GetName().Name is { } name && name.StartsWith("System.", StringComparison.Ordinal);
    }

    // GetFrames is unfiltered: StackTrace.ToString is what honours the attribute, so the walk
    // has to. Enclosing types are checked so that an attribute on an adapter class covers the
    // compiler-generated state machines nested inside it.
    private static bool IsHidden(MethodBase method)
    {
        if (method.IsDefined(typeof(StackTraceHiddenAttribute), inherit: false))
        {
            return true;
        }

        for (var type = method.DeclaringType; type is not null; type = type.DeclaringType)
        {
            if (type.IsDefined(typeof(StackTraceHiddenAttribute), inherit: false))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// The frame in user code an exception left through: the outermost frame that is neither part of
/// this library, the runtime, nor hidden from stack traces, before the first frame of this library.
/// </summary>
/// <param name="Method">The method of the frame.</param>
/// <param name="Line">The source line of the frame, or 0 when no symbols are available.</param>
internal readonly record struct FailureOrigin(MethodBase Method, int Line);
