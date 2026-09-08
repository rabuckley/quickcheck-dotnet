namespace QuickCheck;

/// <summary>
/// Represents an operation on a model and a system under test.
/// </summary>
/// <typeparam name="TModel">The type of the model that stands in for the system during generation.</typeparam>
/// <typeparam name="TSystem">The type of the system under test.</typeparam>
/// <remarks>
/// <para>
/// Generation and each run use separate initial models. Commands must retain only values
/// that remain equal across replays. For mutable models, retain keys or ids and look up objects
/// in the model passed to <see cref="Run"/>; do not retain generation-time model objects.
/// </para>
/// <para>
/// A record implementing this interface prints in a failure report with its members expanded, so
/// commands are best declared as records.
/// </para>
/// </remarks>
public interface ICommand<TModel, TSystem> : IModelCommand<TModel>
{
    /// <summary>
    /// Runs this command against the system under test and asserts what it observes.
    /// </summary>
    /// <param name="model">The model state before the command.</param>
    /// <param name="system">The system under test.</param>
    /// <remarks>
    /// Indicate failure by throwing.
    /// <see cref="IModelCommand{TModel}.Update"/> advances the model only after this
    /// method returns successfully.
    /// </remarks>
    void Run(TModel model, TSystem system);
}
