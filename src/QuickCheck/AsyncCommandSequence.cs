using System.Runtime.CompilerServices;

namespace QuickCheck;

/// <summary>
/// Represents a generated sequence of commands to run asynchronously against a system under test.
/// </summary>
/// <typeparam name="TModel">The type of the model that stands in for the system during generation.</typeparam>
/// <typeparam name="TSystem">The type of the system under test.</typeparam>
public sealed class AsyncCommandSequence<TModel, TSystem>
{
    private readonly Func<TModel> _initialModel;

    internal AsyncCommandSequence(Func<TModel> initialModel, IReadOnlyList<IAsyncCommand<TModel, TSystem>> commands)
    {
        _initialModel = initialModel;
        Commands = commands;
    }

    /// <summary>
    /// Gets the commands in the order they were generated. Each satisfied its precondition in the
    /// model state that the commands before it produce.
    /// </summary>
    public IReadOnlyList<IAsyncCommand<TModel, TSystem>> Commands { get; }

    /// <summary>
    /// Asynchronously runs the commands in order, advancing a fresh model after each command.
    /// </summary>
    /// <param name="system">The system under test.</param>
    /// <param name="invariant">
    /// An assertion over the model and the system that is checked before the first command and
    /// after each one, or <see langword="null"/> to check nothing between commands.
    /// </param>
    /// <returns>The model state after the last command.</returns>
    /// <remarks>
    /// Each run calls the initial-model factory. Each command receives the model state before
    /// its operation. After the command completes successfully,
    /// <see cref="IModelCommand{TModel}.Update"/> advances the model, then the invariant runs.
    /// All operations complete before the next begins. A failure stops the sequence and
    /// propagates through the returned task.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="system"/> is <see langword="null"/>.</exception>
    public Task<TModel> RunAsync(TSystem system, Action<TModel, TSystem>? invariant = null)
    {
        ArgumentNullException.ThrowIfNull(system);

        return RunCoreAsync(system, invariant is null ? null : [MethodImpl(MethodImplOptions.NoOptimization)] (model, system) =>
        {
            invariant(model, system);
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Asynchronously runs the commands in order, advancing a fresh model and awaiting the invariant.
    /// </summary>
    /// <param name="system">The system under test.</param>
    /// <param name="invariant">
    /// An asynchronous assertion over the model and the system that is awaited before the first
    /// command and after each one.
    /// </param>
    /// <returns>The model state after the last command.</returns>
    /// <inheritdoc cref="RunAsync(TSystem, Action{TModel, TSystem})" path="/remarks"/>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="system"/> or <paramref name="invariant"/> is <see langword="null"/>.
    /// </exception>
    public Task<TModel> RunAsync(TSystem system, Func<TModel, TSystem, Task> invariant)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(invariant);

        return RunCoreAsync(system, invariant);
    }

    private async Task<TModel> RunCoreAsync(TSystem system, Func<TModel, TSystem, Task>? invariant)
    {
        var model = _initialModel();

        if (invariant is not null)
        {
            await Check(invariant, model, system).ConfigureAwait(false);
        }

        foreach (var command in Commands)
        {
            await Run(command, model, system).ConfigureAwait(false);
            model = command.Update(model);

            if (invariant is not null)
            {
                await Check(invariant, model, system).ConfigureAwait(false);
            }
        }

        return model;
    }

    // Preserve user frames for FailureKey when a command or invariant throws synchronously.
    // NoOptimization must be on these adapters: on an async method it applies to the stub,
    // not the MoveNext method that calls user code.
    [MethodImpl(MethodImplOptions.NoOptimization)]
    private static Task Run(IAsyncCommand<TModel, TSystem> command, TModel model, TSystem system) =>
        command.RunAsync(model, system);

    [MethodImpl(MethodImplOptions.NoOptimization)]
    private static Task Check(Func<TModel, TSystem, Task> invariant, TModel model, TSystem system) =>
        invariant(model, system);

    /// <summary>Returns the commands, one per line.</summary>
    /// <returns>
    /// Each command formatted as in a failure report, one per line, or <c>[]</c> when the
    /// sequence is empty.
    /// </returns>
    public override string ToString() => CommandSequenceFormat.Format(Commands);
}
