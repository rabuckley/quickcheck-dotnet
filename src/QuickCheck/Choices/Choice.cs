namespace QuickCheck.Choices;

/// <summary>
/// One recorded decision made while generating a value: an unsigned integer in
/// [0, <see cref="Max"/>], where 0 is by convention the simplest option.
/// </summary>
/// <remarks>
/// Every generator is expressed in terms of these choices, so a value can be
/// reproduced by replaying its choices and shrunk by making its choices
/// smaller. This is what lets composed generators (<c>Select</c>,
/// <c>Where</c>, <c>SelectMany</c>) shrink without any per-type shrinker.
/// </remarks>
internal readonly record struct Choice(ulong Value, ulong Max)
{
    public bool IsMinimal => Value == 0;
}

/// <summary>
/// The half-open range [<see cref="Start"/>, <see cref="End"/>) of choices
/// consumed by one nested <see cref="Generator{T}"/> draw. Spans give the shrinker
/// structural boundaries so it can delete a whole list element, or a whole
/// rejected <c>Where</c> attempt, in one step.
/// </summary>
internal readonly record struct ChoiceSpan(int Start, int End)
{
    public int Length => End - Start;
}
