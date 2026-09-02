using QuickCheck.Generators;

namespace QuickCheck;

public static partial class Generate
{
    /// <summary>
    /// Creates a generator for sequences of commands, each drawn from the generator that
    /// <paramref name="command"/> builds for the model state the commands before it produce.
    /// </summary>
    /// <typeparam name="TModel">The type of the model that stands in for the system during generation.</typeparam>
    /// <typeparam name="TSystem">The type of the system under test.</typeparam>
    /// <param name="initialModel">
    /// The factory for the model a sequence starts from. It is called once per generated sequence
    /// and once per <see cref="CommandSequence{TModel, TSystem}.Run"/>, and must return an equal
    /// model on every call.
    /// </param>
    /// <param name="command">
    /// The function that builds the generator of the next command from the current model state,
    /// or returns <see langword="null"/> to end the sequence in that state.
    /// </param>
    /// <param name="maxLength">The inclusive upper bound of the command count. The default is 50.</param>
    /// <returns>
    /// A generator that produces sequences of at most <paramref name="maxLength"/> commands, each
    /// satisfying its precondition in the model state it follows, and that shrinks by removing
    /// commands and shrinking the arguments of the ones that remain.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Generation advances the model alone: for each step it calls <paramref name="command"/>
    /// with the current model, draws a command, and calls
    /// <see cref="ICommand{TModel, TSystem}.Precondition"/> and then
    /// <see cref="ICommand{TModel, TSystem}.Update"/> on it; <see cref="ICommand{TModel, TSystem}.Run"/>
    /// is never called. A command whose precondition is <see langword="false"/> is drawn again,
    /// up to ten times per step, and ten rejections discard the example. To end a sequence in a
    /// state that offers no valid command, return <see langword="null"/> from
    /// <paramref name="command"/> rather than a generator whose every command is rejected.
    /// </para>
    /// <para>
    /// A sequence nearly always has <paramref name="maxLength"/> commands: each step continues
    /// with probability 1 − 2⁻¹⁶, so a shorter sequence is the shrinker's doing, which removes
    /// the commands a failure does not need and finds the shortest failing prefix.
    /// </para>
    /// <para>
    /// Every run of a sequence, including each replay while shrinking, starts from a new model
    /// from <paramref name="initialModel"/>, so a command may hold only what is equal across
    /// replays: a value drawn from an immutable model, or a key or id it looks up in the model
    /// passed to <see cref="ICommand{TModel, TSystem}.Run"/> when the model is mutable. A command
    /// holding an object out of a mutable model holds the generation-time object, whose state is
    /// the model's final state rather than the state at that step.
    /// </para>
    /// <para>
    /// An exception from <paramref name="command"/>, such as <see cref="Elements{T}"/> given no
    /// items, or from a precondition or update, fails the check with that exception.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="initialModel"/> or <paramref name="command"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is less than 1.</exception>
    public static Generator<CommandSequence<TModel, TSystem>> CommandSequence<TModel, TSystem>(
        Func<TModel> initialModel,
        Func<TModel, Generator<ICommand<TModel, TSystem>>?> command,
        int maxLength = 50) =>
        new CommandSequenceGenerator<TModel, TSystem>(initialModel, command, maxLength);
}
