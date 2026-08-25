using System.Numerics;
using QuickCheck.Choices;

namespace QuickCheck.Generators;

/// <summary>
/// Maps a fixed number of choices onto an integer significand in [low, high]: its bit length
/// first, then the value within that length, so the value is log-uniform whatever the width of
/// the range and shrinking drops significant bits before it lowers what remains. A range that
/// may exceed 64 bits draws the value as a high word then a low word, each capped by the range's
/// ends only while the words before it equal that end's, so the shape stays the same on every
/// draw and a replayed prefix never shifts. Choice 0 throughout is <c>low</c>.
/// </summary>
internal readonly struct SignificandRange
{
    private readonly UInt128 _low;
    private readonly UInt128 _high;
    private readonly bool _wide;
    private readonly IntegerRange<int> _lengths;

    /// <param name="low">The inclusive lower bound of the significand.</param>
    /// <param name="high">The inclusive upper bound of the significand.</param>
    /// <param name="wide">
    /// Whether the significand may exceed 64 bits, in which case the value takes two choices
    /// rather than one.
    /// </param>
    public SignificandRange(UInt128 low, UInt128 high, bool wide)
    {
        _low = low;
        _high = high;
        _wide = wide;
        _lengths = new IntegerRange<int>(BitLength(low), BitLength(high));
    }

    public UInt128 Draw(ChoiceSource source)
    {
        var (low, high) = Band(_lengths.Draw(source));
        return _wide ? DrawWide(source, low, high, forced: null) : new IntegerRange<UInt128>(low, high).Draw(source);
    }

    /// <summary>
    /// Emits <paramref name="value"/>, clamped into the range, through the same choices while
    /// generating, and replays the prefix while replaying.
    /// </summary>
    public UInt128 Force(ChoiceSource source, UInt128 value)
    {
        value = UInt128.Clamp(value, _low, _high);
        var (low, high) = Band(_lengths.Force(source, BitLength(value)));
        return _wide ? DrawWide(source, low, high, value) : new IntegerRange<UInt128>(low, high).Force(source, value);
    }

    /// <summary>The part of the range with the given bit length, never empty for a drawn length.</summary>
    private (UInt128 Low, UInt128 High) Band(int bitLength)
    {
        if (bitLength == 0)
        {
            return (UInt128.Zero, UInt128.Zero);
        }

        var bandLow = UInt128.One << (bitLength - 1);
        var bandHigh = bitLength == 128 ? UInt128.MaxValue : (UInt128.One << bitLength) - UInt128.One;
        return (UInt128.Max(_low, bandLow), UInt128.Min(_high, bandHigh));
    }

    private static UInt128 DrawWide(ChoiceSource source, UInt128 low, UInt128 high, UInt128? forced)
    {
        var highWords = new IntegerRange<ulong>((ulong)(low >> 64), (ulong)(high >> 64));
        var highWord = forced is { } forcedHigh ? highWords.Force(source, (ulong)(forcedHigh >> 64)) : highWords.Draw(source);
        var lowWords = new IntegerRange<ulong>(
            highWord == (ulong)(low >> 64) ? (ulong)low : ulong.MinValue,
            highWord == (ulong)(high >> 64) ? (ulong)high : ulong.MaxValue);
        var lowWord = forced is { } forcedLow ? lowWords.Force(source, (ulong)forcedLow) : lowWords.Draw(source);
        return ((UInt128)highWord << 64) | lowWord;
    }

    private static int BitLength(UInt128 value) => 128 - (int)UInt128.LeadingZeroCount(value);
}
