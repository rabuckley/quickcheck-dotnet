using System.Text;

namespace QuickCheck;

/// <summary>
/// Represents a sequence of commands generated against a model by
/// <see cref="Generate.CommandSequence{TModel, TSystem}"/>, ready to run against the system under
/// test.
/// </summary>
/// <typeparam name="TModel">The type of the model that stands in for the system during generation.</typeparam>
/// <typeparam name="TSystem">The type of the system under test.</typeparam>
public sealed class CommandSequence<TModel, TSystem>
{
    private readonly Func<TModel> _initialModel;

    internal CommandSequence(Func<TModel> initialModel, IReadOnlyList<ICommand<TModel, TSystem>> commands)
    {
        _initialModel = initialModel;
        Commands = commands;
    }

    /// <summary>
    /// Gets the commands in the order they were generated. Each satisfied its precondition in the
    /// model state that the commands before it produce.
    /// </summary>
    public IReadOnlyList<ICommand<TModel, TSystem>> Commands { get; }

    /// <summary>
    /// Runs every command against the specified system, in order, advancing a fresh model beside
    /// it.
    /// </summary>
    /// <param name="system">The system under test.</param>
    /// <param name="invariant">
    /// An assertion over the model and the system that is checked before the first command and
    /// after each one, or <see langword="null"/> to check nothing between commands.
    /// </param>
    /// <returns>The model state after the last command.</returns>
    /// <remarks>
    /// The model starts as a new initial model from the sequence's factory. For each command,
    /// <see cref="ICommand{TModel, TSystem}.Run"/> is called with the model state before the
    /// command, then <see cref="ICommand{TModel, TSystem}.Update"/> advances the model, then
    /// <paramref name="invariant"/> sees the state after it. An exception from a command or the
    /// invariant propagates, which is how a failure is reported.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="system"/> is <see langword="null"/>.</exception>
    public TModel Run(TSystem system, Action<TModel, TSystem>? invariant = null)
    {
        ArgumentNullException.ThrowIfNull(system);

        var model = _initialModel();
        invariant?.Invoke(model, system);

        foreach (var command in Commands)
        {
            command.Run(model, system);
            model = command.Update(model);
            invariant?.Invoke(model, system);
        }

        return model;
    }

    /// <summary>Returns the commands, one per line.</summary>
    /// <returns>
    /// Each command formatted as in a failure report, one per line, or <c>[]</c> when the
    /// sequence is empty.
    /// </returns>
    public override string ToString()
    {
        if (Commands.Count == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder();

        for (var i = 0; i < Commands.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
            }

            builder.Append(ValueFormatter.Format(Commands[i]));
        }

        return builder.ToString();
    }
}
