using QuickCheck.Choices;

namespace QuickCheck.Running;

/// <summary>
/// Minimises a failing example by editing its choice sequence and replaying
/// it, keeping any candidate that fails the same way and is simpler in
/// shortlex order (fewer choices, or equal length and lexicographically
/// smaller values). That order strictly decreases with every accepted
/// candidate, which guarantees termination.
/// </summary>
internal sealed class Shrinker<T>
{
    private readonly Generator<T> _generator;
    private readonly Func<T, ValueTask<bool>> _body;
    private readonly FailureKey _key;
    private readonly ShrinkBudget _budget;
    private readonly CancellationToken _cancellationToken;

    private ExampleRun<T> _best;
    private int _shrinks;

    public Shrinker(
        Generator<T> generator,
        Func<T, ValueTask<bool>> body,
        ExampleRun<T> failure,
        CheckOptions options,
        CancellationToken cancellationToken)
    {
        _generator = generator;
        _body = body;
        _best = failure;
        _key = failure.Key;
        _budget = new ShrinkBudget(options.MaxShrinkAttempts, options.MaxShrinkWork);
        _cancellationToken = cancellationToken;
    }

    public async ValueTask<ShrinkOutcome<T>> RunAsync()
    {
        _cancellationToken.ThrowIfCancellationRequested();

        bool improved;

        do
        {
            improved = await DeleteSpansAsync().ConfigureAwait(false);
            improved |= await ZeroSpansAsync().ConfigureAwait(false);
            improved |= await MinimiseDuplicatesAsync().ConfigureAwait(false);
            improved |= await MinimiseChoicesAsync().ConfigureAwait(false);
            improved |= await RedistributePairsAsync().ConfigureAwait(false);
        } while (improved && !_budget.Exhausted);

        return new ShrinkOutcome<T>
        {
            Minimal = _best,
            Attempts = _budget.Attempts,
            Shrinks = _shrinks,
            Limit = _budget.LimitReached
        };
    }

    /// <summary>
    /// Tries removing each structural span outright — a list element, a
    /// rejected filter attempt, a whole subtree.
    /// </summary>
    private async ValueTask<bool> DeleteSpansAsync()
    {
        var improved = false;

        for (var i = _best.Spans.Count - 1; i >= 0; i--)
        {
            if (_budget.Exhausted)
            {
                break;
            }

            var span = _best.Spans[i];
            var candidate = new List<Choice>(_best.Choices.Count - span.Length);

            for (var j = 0; j < _best.Choices.Count; j++)
            {
                if (j < span.Start || j >= span.End)
                {
                    candidate.Add(_best.Choices[j]);
                }
            }

            if (await TryAcceptAsync(candidate).ConfigureAwait(false))
            {
                improved = true;
                // The span list was rebuilt from the accepted replay; carry on
                // from the same position, clamped to the new count.
                i = Math.Min(i, _best.Spans.Count);
            }
        }

        return improved;
    }

    /// <summary>
    /// Tries setting every choice within a span to its minimum at once, for
    /// values whose choices only shrink together.
    /// </summary>
    private async ValueTask<bool> ZeroSpansAsync()
    {
        var improved = false;

        for (var i = _best.Spans.Count - 1; i >= 0; i--)
        {
            if (_budget.Exhausted)
            {
                break;
            }

            var span = _best.Spans[i];
            var alreadyMinimal = true;

            for (var j = span.Start; j < span.End; j++)
            {
                alreadyMinimal &= _best.Choices[j].IsMinimal;
            }

            if (alreadyMinimal)
            {
                continue;
            }

            var candidate = new List<Choice>(_best.Choices);

            for (var j = span.Start; j < span.End; j++)
            {
                candidate[j] = candidate[j] with { Value = 0 };
            }

            if (await TryAcceptAsync(candidate).ConfigureAwait(false))
            {
                improved = true;
                i = Math.Min(i, _best.Spans.Count);
            }
        }

        return improved;
    }

    /// <summary>
    /// Shrinks every group of choices sharing the same value in lockstep, for
    /// failures that depend on values being equal (a duplicate in a list).
    /// </summary>
    private async ValueTask<bool> MinimiseDuplicatesAsync()
    {
        var improved = false;

        var groups = _best.Choices
            .Select(static (choice, index) => (choice, index))
            .Where(static pair => !pair.choice.IsMinimal)
            .GroupBy(static pair => pair.choice)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Select(static pair => pair.index).ToArray())
            .ToList();

        foreach (var indices in groups)
        {
            if (_budget.Exhausted)
            {
                break;
            }

            // An accepted candidate may have shortened the sequence out from
            // under the groups that follow it; those are stale, the rest are not.
            if (indices.Any(index => index >= _best.Choices.Count))
            {
                continue;
            }

            var low = 0UL;
            var high = _best.Choices[indices[0]].Value;

            if (await TryReplaceAllAsync(indices, 0).ConfigureAwait(false))
            {
                improved = true;
                continue;
            }

            while (high - low > 1 && !_budget.Exhausted)
            {
                var mid = low + (high - low) / 2;

                if (await TryReplaceAllAsync(indices, mid).ConfigureAwait(false))
                {
                    improved = true;
                    high = mid;
                }
                else
                {
                    low = mid;
                }
            }
        }

        return improved;
    }

    private async ValueTask<bool> TryReplaceAllAsync(int[] indices, ulong value)
    {
        var candidate = new List<Choice>(_best.Choices);

        foreach (var index in indices)
        {
            if (index >= candidate.Count)
            {
                return false;
            }

            candidate[index] = candidate[index] with { Value = value };
        }

        return await TryAcceptAsync(candidate).ConfigureAwait(false);
    }

    /// <summary>
    /// For nearby pairs of choices, moves value from the earlier to the later
    /// one, for failures that depend on a sum or difference (so that
    /// <c>a + b >= 100</c> ends at <c>(0, 100)</c> rather than <c>(100, 0)</c>).
    /// </summary>
    private async ValueTask<bool> RedistributePairsAsync()
    {
        const int window = 4;
        var improved = false;

        for (var i = 0; i < _best.Choices.Count && !_budget.Exhausted; i++)
        {
            for (var j = i + 1; j <= i + window && j < _best.Choices.Count; j++)
            {
                var first = _best.Choices[i];
                var second = _best.Choices[j];

                if (first.IsMinimal || second.Value == second.Max)
                {
                    continue;
                }

                // Try moving everything, then halve until a transfer reproduces.
                // Whatever is accepted leaves the pair to be revisited by the
                // next pass, which moves more of what is left.
                var maxTransfer = Math.Min(first.Value, second.Max - second.Value);

                if (await TryTransferAsync(i, j, maxTransfer).ConfigureAwait(false))
                {
                    improved = true;
                    continue;
                }

                var low = 0UL;
                var high = maxTransfer;

                while (high - low > 1 && !_budget.Exhausted)
                {
                    var mid = low + (high - low) / 2;

                    if (await TryTransferAsync(i, j, mid).ConfigureAwait(false))
                    {
                        improved = true;
                        break;
                    }

                    high = mid;
                }
            }
        }

        return improved;
    }

    private async ValueTask<bool> TryTransferAsync(int from, int to, ulong amount)
    {
        if (amount == 0 || from >= _best.Choices.Count || to >= _best.Choices.Count)
        {
            return false;
        }

        var candidate = new List<Choice>(_best.Choices);
        candidate[from] = candidate[from] with { Value = candidate[from].Value - amount };
        candidate[to] = candidate[to] with { Value = candidate[to].Value + amount };
        return await TryAcceptAsync(candidate).ConfigureAwait(false);
    }

    /// <summary>
    /// Shrinks each choice towards zero individually: a binary search, then a
    /// short linear scan for predicates the binary search cannot see through
    /// (such as a filter that only accepts every third value).
    /// </summary>
    private async ValueTask<bool> MinimiseChoicesAsync()
    {
        var improved = false;

        for (var i = 0; i < _best.Choices.Count && !_budget.Exhausted; i++)
        {
            if (_best.Choices[i].IsMinimal)
            {
                continue;
            }

            if (await TryReplaceAsync(i, 0).ConfigureAwait(false))
            {
                improved = true;
                continue;
            }

            while (!_budget.Exhausted && i < _best.Choices.Count && !_best.Choices[i].IsMinimal)
            {
                improved |= await BinarySearchChoiceAsync(i).ConfigureAwait(false);

                if (!await StepChoiceDownAsync(i).ConfigureAwait(false))
                {
                    break;
                }

                improved = true;
            }
        }

        return improved;
    }

    /// <summary>
    /// Finds a smaller reproducing value for the choice at
    /// <paramref name="index"/> assuming failure is monotone in the choice:
    /// <c>low</c> does not reproduce, <c>high</c> does.
    /// </summary>
    private async ValueTask<bool> BinarySearchChoiceAsync(int index)
    {
        var improved = false;
        var low = 0UL;
        var high = _best.Choices[index].Value;

        while (high - low > 1 && !_budget.Exhausted)
        {
            var mid = low + (high - low) / 2;

            if (await TryReplaceAsync(index, mid).ConfigureAwait(false))
            {
                improved = true;
                // The accepted replay may have restructured the sequence;
                // continue from the value now at this position.
                high = index < _best.Choices.Count ? _best.Choices[index].Value : 0;

                if (high <= low)
                {
                    break;
                }
            }
            else
            {
                low = mid;
            }
        }

        return improved;
    }

    private const int MaxLinearSteps = 8;

    private async ValueTask<bool> StepChoiceDownAsync(int index)
    {
        for (var step = 1UL; step <= MaxLinearSteps && !_budget.Exhausted; step++)
        {
            if (index >= _best.Choices.Count || _best.Choices[index].Value < step)
            {
                return false;
            }

            if (await TryReplaceAsync(index, _best.Choices[index].Value - step).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async ValueTask<bool> TryReplaceAsync(int index, ulong value)
    {
        if (index >= _best.Choices.Count || _best.Choices[index].Value == value)
        {
            return false;
        }

        var replaced = new List<Choice>(_best.Choices);
        replaced[index] = replaced[index] with { Value = value };

        if (await TryAcceptAsync(replaced).ConfigureAwait(false))
        {
            return true;
        }

        var current = _best.Choices[index].Value;

        return value < current && await TryReplaceAsLengthAsync(index, replaced, current - value).ConfigureAwait(false);
    }

    /// <summary>
    /// A lowered choice may be a length: the surplus elements then follow it
    /// as a chain of adjacent spans, and the example only stays failing if the
    /// right ones are removed. Tries dropping the first <paramref name="delta"/>
    /// spans of each such chain.
    /// </summary>
    private async ValueTask<bool> TryReplaceAsLengthAsync(int index, List<Choice> replaced, ulong delta)
    {
        const int maxChainsToTry = 3;
        var chainsTried = 0;

        foreach (var chain in AdjacentSpanChains(index + 1))
        {
            if ((ulong)chain.Count < delta)
            {
                continue;
            }

            if (chainsTried == maxChainsToTry || _budget.Exhausted)
            {
                break;
            }

            chainsTried++;

            var deleteEnd = chain[(int)delta - 1].End;
            var candidate = new List<Choice>(replaced.Count);

            for (var j = 0; j < replaced.Count; j++)
            {
                if (j <= index || j >= deleteEnd)
                {
                    candidate.Add(replaced[j]);
                }
            }

            if (await TryAcceptAsync(candidate).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// For each span starting at <paramref name="start"/> (innermost first),
    /// the run of spans that follow it back-to-back.
    /// </summary>
    private IEnumerable<List<ChoiceSpan>> AdjacentSpanChains(int start)
    {
        var spans = _best.Spans;
        var heads = spans.Where(span => span.Start == start).OrderBy(span => span.Length);

        foreach (var head in heads)
        {
            var chain = new List<ChoiceSpan> { head };
            var current = head;

            while (true)
            {
                var found = false;
                var next = default(ChoiceSpan);

                foreach (var span in spans)
                {
                    if (span.Start == current.End && (!found || span.Length < next.Length))
                    {
                        next = span;
                        found = true;
                    }
                }

                if (!found)
                {
                    break;
                }

                chain.Add(next);
                current = next;
            }

            yield return chain;
        }
    }

    private async ValueTask<bool> TryAcceptAsync(IReadOnlyList<Choice> candidate)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        if (!_budget.TryCharge(candidate.Count))
        {
            return false;
        }

        var run = await ExampleRun<T>
            .ExecuteAsync(ChoiceSource.FromPrefix(candidate), _generator, _body, _cancellationToken)
            .ConfigureAwait(false);

        if (!run.IsFailure || run.Key != _key || !IsSimpler(run.Choices, _best.Choices))
        {
            return false;
        }

        _best = run;
        _shrinks++;
        return true;
    }

    private static bool IsSimpler(IReadOnlyList<Choice> candidate, IReadOnlyList<Choice> current)
    {
        if (candidate.Count != current.Count)
        {
            return candidate.Count < current.Count;
        }

        for (var i = 0; i < candidate.Count; i++)
        {
            if (candidate[i].Value != current[i].Value)
            {
                return candidate[i].Value < current[i].Value;
            }
        }

        return false;
    }
}
