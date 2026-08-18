namespace QuickCheck.Running;

/// <summary>
/// Identifies how an example failed.
/// </summary>
/// <param name="ExceptionType">
/// The type of exception thrown, or <see langword="null"/> when the property returned <see langword="false"/>.
/// </param>
internal readonly record struct FailureKey(Type? ExceptionType);
