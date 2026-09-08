namespace QuickCheck;

/// <summary>
/// Represents an asynchronous operation on a model and a system under test.
/// </summary>
/// <typeparam name="TModel">The type of the model that stands in for the system during generation.</typeparam>
/// <typeparam name="TSystem">The type of the system under test.</typeparam>
/// <remarks>
/// <para>
/// Generation and each run use separate initial models. Commands must retain only values
/// that remain equal across replays. For mutable models, retain keys or ids and look up objects
/// in the model passed to <see cref="RunAsync"/>; do not retain generation-time model objects.
/// </para>
/// <para>
/// A record implementing this interface prints in a failure report with its members expanded, so
/// commands are best declared as records.
/// </para>
/// </remarks>
public interface IAsyncCommand<TModel, TSystem> : IModelCommand<TModel>
{
    /// <summary>
    /// Asynchronously runs this command against the system under test and asserts what it observes.
    /// </summary>
    /// <param name="model">The model state before the command.</param>
    /// <param name="system">The system under test.</param>
    /// <remarks>
    /// <para>
    /// Indicate failure by throwing or returning a faulted task.
    /// <see cref="IModelCommand{TModel}.Update"/> advances the model only after this
    /// method completes successfully.
    /// </para>
    /// <para>
    /// A command that needs a <see cref="CancellationToken"/> can obtain it from
    /// <paramref name="system"/>. The check's cancellation token is not passed to commands.
    /// </para>
    /// </remarks>
    Task RunAsync(TModel model, TSystem system);
}
