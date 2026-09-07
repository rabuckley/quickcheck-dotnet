using QuickCheck.Running;

namespace QuickCheck.Tests.Running;

public sealed class CoverageLookTests
{
    [Fact]
    public void Verdict_WithOneRareHit_ShouldRemainUndecided()
    {
        var look = new CoverageLook(Confidence.Default, passed: 100, look: 0);

        Assert.Equal(CoverageVerdict.Undecided, look.Verdict(minimumPercent: 0.02, count: 1));
    }

    [Theory]
    [InlineData(2L, 0)]
    [InlineData(1_000_000_000L, 0)]
    [InlineData(1_000_000_000L, 9)]
    [InlineData(long.MaxValue, 50)]
    public void Verdict_AcrossAllBinomialOutcomes_ShouldRespectEachTailBudget(long certainty, int lookIndex)
    {
        // Sum exact binomial masses independently of the decision's tail bound. At the two
        // null boundaries these are the largest error probabilities outside the tolerance band.
        var confidence = new Confidence { Certainty = certainty };
        var tailBudget = Math.ScaleB(1.0 / certainty, -(lookIndex + 2));
        foreach (var total in new[] { 100, 400 })
        {
            var look = new CoverageLook(confidence, total, lookIndex);
            foreach (var minimum in new[] { 0.0002, 0.01, 0.1, 0.5, 0.9, 0.9999 })
            {
                double falseAcceptance = 0;
                double falseRejection = 0;
                double logBinomialCoefficient = 0;
                for (var count = 0; count <= total; count++)
                {
                    var verdict = look.Verdict(minimum * 100, count);
                    if (verdict == CoverageVerdict.Met)
                    {
                        var probability = confidence.Tolerance * minimum;
                        falseAcceptance += Math.Exp(logBinomialCoefficient + count * Math.Log(probability)
                            + (total - count) * Math.Log(1 - probability));
                    }
                    else if (verdict == CoverageVerdict.Unmet)
                    {
                        falseRejection += Math.Exp(logBinomialCoefficient + count * Math.Log(minimum)
                            + (total - count) * Math.Log(1 - minimum));
                    }

                    if (count < total)
                    {
                        logBinomialCoefficient += Math.Log((double)(total - count) / (count + 1));
                    }
                }

                Assert.True(falseAcceptance <= tailBudget,
                    $"Acceptance error {falseAcceptance} exceeds {tailBudget} at n={total}, p={minimum}");
                Assert.True(falseRejection <= tailBudget,
                    $"Rejection error {falseRejection} exceeds {tailBudget} at n={total}, p={minimum}");
            }
        }
    }

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(150, 100, false)]
    [InlineData(200, 100, true)]
    [InlineData(300, 100, false)]
    [InlineData(400, 100, true)]
    [InlineData(150, 150, true)]
    [InlineData(1, 1, true)]
    [InlineData(50, 100, false)]
    public void IsDue_WithPassCount_ShouldBeTrueAtRunCountAndAtHundredTimesAPowerOfTwo(int passed, int runCount, bool expected)
    {
        // Act
        var isDue = CoverageLook.IsDue(passed, runCount);

        // Assert
        Assert.Equal(expected, isDue);
    }

    [Fact]
    public void Verdict_WithNoHitsAtTheFirstLook_ShouldRejectAHalfButNotYetATenth()
    {
        // Arrange
        var look = new CoverageLook(Confidence.Default, passed: 100, look: 0);

        // Act
        var half = look.Verdict(minimumPercent: 50, count: 0);
        var tenth = look.Verdict(minimumPercent: 10, count: 0);

        // Assert
        Assert.Equal(CoverageVerdict.Unmet, half);
        Assert.Equal(CoverageVerdict.Undecided, tenth);
    }

    [Fact]
    public void Verdict_WithRateKnownToLieInsideTheToleranceBand_ShouldBeMet()
    {
        // Arrange: after 51,200 examples at 47% the interval for a 50% minimum lies wholly inside
        // 45% to 50%, so the rate is known short of the minimum and known within tolerance.
        var look = new CoverageLook(Confidence.Default, passed: 51_200, look: 9);

        // Act
        var verdict = look.Verdict(minimumPercent: 50, count: 24_064);

        // Assert
        Assert.Equal(CoverageVerdict.Met, verdict);
    }
}
