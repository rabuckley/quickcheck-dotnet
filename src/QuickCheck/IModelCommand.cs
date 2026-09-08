namespace QuickCheck;

/// <summary>
/// Represents what a command sequence generator needs of one operation: whether it may follow
/// the model state it is generated in, and how it changes that model.
/// <see cref="ICommand{TModel, TSystem}"/> adds how the operation runs against the real system.
/// </summary>
/// <typeparam name="TModel">The type of the model that stands in for the system during generation.</typeparam>
public interface IModelCommand<TModel>
{
    /// <summary>
    /// Determines whether this command may be generated in the specified model state.
    /// </summary>
    /// <param name="model">The model state the command would follow.</param>
    /// <returns>
    /// <see langword="true"/> if the command may follow <paramref name="model"/>; otherwise,
    /// <see langword="false"/>. The default implementation returns <see langword="true"/>.
    /// </returns>
    /// <remarks>
    /// This method must not change <paramref name="model"/>. A command whose precondition is
    /// <see langword="false"/> is drawn again, up to ten times per step, before the example is
    /// discarded.
    /// </remarks>
    bool Precondition(TModel model) => true;

    /// <summary>
    /// Advances the model past this command.
    /// </summary>
    /// <param name="model">The model state before the command.</param>
    /// <returns>
    /// The model state after the command: <paramref name="model"/> itself when it was changed in
    /// place, or a new model when the model type is immutable.
    /// </returns>
    TModel Update(TModel model);
}
