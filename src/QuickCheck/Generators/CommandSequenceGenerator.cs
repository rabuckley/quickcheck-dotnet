using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Generates command sequences with the span layout of <see cref="ListGenerator{T}"/>, one step
/// per span, so they shrink the same way: by deleting steps and truncating. Later steps are
/// regenerated against the shrunk model, which re-evaluates their preconditions.
/// </summary>
internal sealed class CommandSequenceGenerator<TModel, TCommand, TSequence> : Generator<TSequence>
    where TCommand : class, IModelCommand<TModel>
{
    // Hypothesis's stateful_step_count continuation: a sequence nearly always reaches maxLength,
    // and the shrinker, not the length draw, finds the shortest failing prefix. The 1-in-65,536
    // chance of stopping early is what lets a truncated choice sequence still parse.
    private const double ContinueProbability = 1 - 1.0 / 65_536;

    private readonly Func<TModel> _initialModel;
    private readonly Func<TModel, Generator<TCommand>?> _command;
    private readonly int _maxLength;
    private readonly Func<Func<TModel>, IReadOnlyList<TCommand>, TSequence> _create;

    public CommandSequenceGenerator(
        Func<TModel> initialModel,
        Func<TModel, Generator<TCommand>?> command,
        int maxLength,
        Func<Func<TModel>, IReadOnlyList<TCommand>, TSequence> create)
    {
        ArgumentNullException.ThrowIfNull(initialModel);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

        _initialModel = initialModel;
        _command = command;
        _maxLength = maxLength;
        _create = create;
    }

    protected internal override TSequence Generate(ChoiceSource source)
    {
        var model = _initialModel();
        var commands = new List<TCommand>();

        while (commands.Count < _maxLength)
        {
            // The step closes over the current model, so it is built per step: a generator
            // instance is shared and may be drawn from concurrently.
            var step = QuickCheck.Generate.From(stepSource => DrawStep(stepSource, model, commands.Count));
            var command = source.Draw(step);

            if (command is null)
            {
                break;
            }

            commands.Add(command);
            model = command.Update(model);
        }

        return _create(_initialModel, commands);
    }

    private TCommand? DrawStep(ChoiceSource source, TModel model, int step)
    {
        if (!source.NextBoolean(ContinueProbability))
        {
            return null;
        }

        var next = _command(model);

        if (next is null)
        {
            return null;
        }

        for (var attempt = 0; attempt < QuickCheck.Generate.MaxFilterAttempts; attempt++)
        {
            var candidate = source.Draw(next)
                ?? throw new InvalidOperationException($"The command generator produced null at step {step}.");

            if (candidate.Precondition(model))
            {
                return candidate;
            }
        }

        throw new DiscardException(
            $"No command satisfied its precondition after {QuickCheck.Generate.MaxFilterAttempts} attempts at step {step}.");
    }
}
