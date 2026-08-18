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
    private readonly Func<T, bool> _body;
    private readonly FailureKey _key;
    private readonly int _maxAttempts;

    private ExampleRun<T> _best;

    public Shrinker(Generator<T> generator, Func<T, bool> body, ExampleRun<T> failure, int maxAttempts)
    {
        _generator = generator;
        _body = body;
        _best = failure;
        _key = failure.Key;
        _maxAttempts = maxAttempts;
    }

    public ExampleRun<T> Best => _best;
    public int Attempts { get; private set; }
    public int Shrinks { get; private set; }

    public ExampleRun<T> Run()
    {
        bool improved;

        do
        {
            improved = DeleteSpans();
            improved |= ZeroSpans();
            improved |= MinimiseDuplicates();
            improved |= MinimiseChoices();
            improved |= RedistributePairs();
        } while (improved && Attempts < _maxAttempts);

        return _best;
    }

    /// <summary>
    /// Tries removing each structural span outright — a list element, a
    /// rejected filter attempt, a whole subtree.
    /// </summary>
    private bool DeleteSpans()
    {
        var improved = false;

        for (var i = _best.Spans.Count - 1; i >= 0; i--)
        {
            if (Attempts >= _maxAttempts)
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

            if (TryAccept(candidate))
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
    private bool ZeroSpans()
    {
        var improved = false;

        for (var i = _best.Spans.Count - 1; i >= 0; i--)
        {
            if (Attempts >= _maxAttempts)
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

            if (TryAccept(candidate))
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
    private bool MinimiseDuplicates()
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
            if (Attempts >= _maxAttempts)
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

            if (TryReplaceAll(indices, 0))
            {
                improved = true;
                continue;
            }

            while (high - low > 1 && Attempts < _maxAttempts)
            {
                var mid = low + (high - low) / 2;

                if (TryReplaceAll(indices, mid))
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

    private bool TryReplaceAll(int[] indices, ulong value)
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

        return TryAccept(candidate);
    }

    /// <summary>
    /// For nearby pairs of choices, moves value from the earlier to the later
    /// one, for failures that depend on a sum or difference (so that
    /// <c>a + b >= 100</c> ends at <c>(0, 100)</c> rather than <c>(100, 0)</c>).
    /// </summary>
    private bool RedistributePairs()
    {
        const int window = 4;
        var improved = false;

        for (var i = 0; i < _best.Choices.Count && Attempts < _maxAttempts; i++)
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

                if (TryTransfer(i, j, maxTransfer))
                {
                    improved = true;
                    continue;
                }

                var low = 0UL;
                var high = maxTransfer;

                while (high - low > 1 && Attempts < _maxAttempts)
                {
                    var mid = low + (high - low) / 2;

                    if (TryTransfer(i, j, mid))
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

    private bool TryTransfer(int from, int to, ulong amount)
    {
        if (amount == 0 || from >= _best.Choices.Count || to >= _best.Choices.Count)
        {
            return false;
        }

        var candidate = new List<Choice>(_best.Choices);
        candidate[from] = candidate[from] with { Value = candidate[from].Value - amount };
        candidate[to] = candidate[to] with { Value = candidate[to].Value + amount };
        return TryAccept(candidate);
    }

    /// <summary>
    /// Shrinks each choice towards zero individually: a binary search, then a
    /// short linear scan for predicates the binary search cannot see through
    /// (such as a filter that only accepts every third value).
    /// </summary>
    private bool MinimiseChoices()
    {
        var improved = false;

        for (var i = 0; i < _best.Choices.Count && Attempts < _maxAttempts; i++)
        {
            if (_best.Choices[i].IsMinimal)
            {
                continue;
            }

            if (TryReplace(i, 0))
            {
                improved = true;
                continue;
            }

            while (Attempts < _maxAttempts && i < _best.Choices.Count && !_best.Choices[i].IsMinimal)
            {
                improved |= BinarySearchChoice(i);

                if (!StepChoiceDown(i))
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
    private bool BinarySearchChoice(int index)
    {
        var improved = false;
        var low = 0UL;
        var high = _best.Choices[index].Value;

        while (high - low > 1 && Attempts < _maxAttempts)
        {
            var mid = low + (high - low) / 2;

            if (TryReplace(index, mid))
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

    private bool StepChoiceDown(int index)
    {
        for (var step = 1UL; step <= MaxLinearSteps && Attempts < _maxAttempts; step++)
        {
            if (index >= _best.Choices.Count || _best.Choices[index].Value < step)
            {
                return false;
            }

            if (TryReplace(index, _best.Choices[index].Value - step))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryReplace(int index, ulong value)
    {
        if (index >= _best.Choices.Count || _best.Choices[index].Value == value)
        {
            return false;
        }

        var replaced = new List<Choice>(_best.Choices);
        replaced[index] = replaced[index] with { Value = value };

        if (TryAccept(replaced))
        {
            return true;
        }

        var current = _best.Choices[index].Value;

        return value < current && TryReplaceAsLength(index, replaced, current - value);
    }

    /// <summary>
    /// A lowered choice may be a length: the surplus elements then follow it
    /// as a chain of adjacent spans, and the example only stays failing if the
    /// right ones are removed. Tries dropping the first <paramref name="delta"/>
    /// spans of each such chain.
    /// </summary>
    private bool TryReplaceAsLength(int index, List<Choice> replaced, ulong delta)
    {
        const int maxChainsToTry = 3;
        var chainsTried = 0;

        foreach (var chain in AdjacentSpanChains(index + 1))
        {
            if ((ulong)chain.Count < delta)
            {
                continue;
            }

            if (chainsTried == maxChainsToTry || Attempts >= _maxAttempts)
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

            if (TryAccept(candidate))
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

    private bool TryAccept(IReadOnlyList<Choice> candidate)
    {
        if (Attempts >= _maxAttempts)
        {
            return false;
        }

        Attempts++;

        var run = ExampleRun<T>.Execute(ChoiceSource.FromPrefix(candidate), _generator, _body);

        if (!run.IsFailure || run.Key != _key || !IsSimpler(run.Choices, _best.Choices))
        {
            return false;
        }

        _best = run;
        Shrinks++;
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
