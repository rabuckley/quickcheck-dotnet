namespace QuickCheck.Tests;

public sealed class AsyncPropertyTests
{
    [Fact]
    public async Task CheckAsync_WithAsyncBody_ShouldAwaitEveryExample()
    {
        // Arrange
        var seen = 0;
        var property = Property.ForAll(Generate.Integer<int>(), async x =>
        {
            await Task.Yield();
            Interlocked.Increment(ref seen);
            _ = x;
        });

        // Act
        var result = await property.CheckAsync(new CheckOptions { RunCount = 30 });

        // Assert
        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(30, seen);
    }

    /// <summary>
    /// A throwing body is the case that tells an awaited body from one bound as <c>async void</c>:
    /// the exception of an unawaited body is never seen, so the property would pass.
    /// </summary>
    [Fact]
    public async Task CheckAsync_WithAsyncBodyThatThrows_ShouldFalsifyTheProperty()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), async x =>
        {
            await Task.Yield();

            if (x > 10)
            {
                throw new InvalidOperationException("too big");
            }
        });

        // Act
        var result = await property.CheckAsync(new CheckOptions { Seed = 1, RunCount = 1000 });

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal(11, result.Minimal.Value);
    }

    [Fact]
    public async Task CheckAsync_WithExceptionFromAsyncBody_ShouldReportItUnwrapped()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>(), Generate.Integer<int>(), async (a, b) =>
        {
            await Task.Yield();
            throw new InvalidOperationException("thrown from the body");
        });

        // Act
        var result = await property.CheckAsync(new CheckOptions { RunCount = 1 });

        // Assert
        Assert.True(result.IsFalsified);
        var exception = Assert.IsType<InvalidOperationException>(result.Minimal.Exception);
        Assert.Equal("thrown from the body", exception.Message);
    }

    [Fact]
    public async Task CheckAsync_WithAsyncFailure_ShouldShrinkItLikeASynchronousOne()
    {
        // Arrange
        var property = Property.ForAll(Generate.Integer<int>().List(), async items =>
        {
            await Task.Yield();
            return items.Sum(static x => (long)x) < 100;
        });
        var options = new CheckOptions { Seed = 7, RunCount = 500 };

        // Act
        var result = await property.CheckAsync(options);
        var exception = await Assert.ThrowsAsync<PropertyFailedException>(async () => await property.AssertAsync(options));

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Equal([100], result.Minimal.Value);
        Assert.Contains("Minimal counterexample: [100]", exception.Message);
    }

    /// <summary>
    /// Every asynchronous <c>ForAll</c> overload, so that an arity or body shape cannot be dropped
    /// or shadowed by the synchronous overloads without a test failing to compile.
    /// </summary>
    [Fact]
    public async Task ForAll_WithEveryAsynchronousOverload_ShouldAcceptAndCheckIt()
    {
        // Arrange
        var integers = Generate.Integer<int>();
        var booleans = Generate.Boolean();

        // Act
        var outcomes = new[]
        {
            (await Property.ForAll(integers, async x =>
            {
                await Task.Yield();
                _ = x;
            }).CheckAsync()).Outcome,

            (await Property.ForAll(integers, async x =>
            {
                await Task.Yield();
                return x - x == 0;
            }).CheckAsync()).Outcome,

            (await Property.ForAll(integers, integers, async (a, b) =>
            {
                await Task.Yield();
                _ = a + b;
            }).CheckAsync()).Outcome,

            (await Property.ForAll(integers, integers, async (a, b) =>
            {
                await Task.Yield();
                return a + b == b + a;
            }).CheckAsync()).Outcome,

            (await Property.ForAll(booleans, booleans, booleans, async (a, b, c) =>
            {
                await Task.Yield();
                _ = a | b | c;
            }).CheckAsync()).Outcome,

            (await Property.ForAll(booleans, booleans, booleans, async (a, b, c) =>
            {
                await Task.Yield();
                return ((a | b) | c) == (a | (b | c));
            }).CheckAsync()).Outcome
        };

        // Assert
        Assert.All(outcomes, static outcome => Assert.Equal(PropertyOutcome.Passed, outcome));
    }

    [Fact]
    public void Check_WithCancellation_ShouldAbortBetweenExamples()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var runs = 0;
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            if (++runs == 5)
            {
                cancellation.Cancel();
            }
        });

        // Act
        var exception = Record.Exception(() => property.Check(new CheckOptions { RunCount = 1000 }, cancellation.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(5, runs);
    }

    [Fact]
    public async Task CheckAsync_WithCancellation_ShouldAbortBetweenExamples()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var runs = 0;
        var property = Property.ForAll(Generate.Integer<int>(), async _ =>
        {
            await Task.Yield();

            if (++runs == 5)
            {
                await cancellation.CancelAsync();
            }
        });

        // Act
        var exception = await Record.ExceptionAsync(() => property.CheckAsync(new CheckOptions { RunCount = 1000 }, cancellation.Token));

        // Assert
        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(5, runs);
    }

    /// <summary>
    /// A body that abandons the check because the check's own token was cancelled aborts it, rather
    /// than having its <see cref="OperationCanceledException"/> recorded as a counterexample — with
    /// no shrink attempts, which are the other place the token is observed.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(10_000)]
    public void Check_WithCancellationFromTheBody_ShouldAbortTheCheck(int maxShrinkAttempts)
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var abandoned = new OperationCanceledException("abandoned", cancellation.Token);
        var property = Property.ForAll(Generate.Integer<int>(), void (_) =>
        {
            cancellation.Cancel();
            throw abandoned;
        });

        // Act
        var thrown = Assert.ThrowsAny<OperationCanceledException>(() =>
            property.Check(new CheckOptions { MaxShrinkAttempts = maxShrinkAttempts }, cancellation.Token));

        // Assert
        Assert.Same(abandoned, thrown);
    }

    [Fact]
    public async Task CheckAsync_WithCancellationFromTheBody_ShouldAbortTheCheck()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var property = Property.ForAll(Generate.Integer<int>(), async _ =>
        {
            await Task.Yield();
            await cancellation.CancelAsync();
            cancellation.Token.ThrowIfCancellationRequested();
        });

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => property.CheckAsync(new CheckOptions { MaxShrinkAttempts = 0 }, cancellation.Token));
    }

    /// <summary>
    /// Cancellation requested by the failing example itself is observed on the way into shrinking,
    /// where there may be no attempt left to observe it.
    /// </summary>
    [Fact]
    public void Check_WithCancellationRequestedByTheFailingExample_ShouldAbortRatherThanReport()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            cancellation.Cancel();
            return false;
        });

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() =>
            property.Check(new CheckOptions { MaxShrinkAttempts = 0 }, cancellation.Token));
    }

    [Fact]
    public void Check_WithCancellationDuringShrinking_ShouldPropagateIt()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var failures = 0;
        var property = Property.ForAll(Generate.Integer<int>(), x =>
        {
            if (x > 10 && ++failures == 3)
            {
                cancellation.Cancel();
            }

            return x <= 10;
        });

        // Act & Assert
        Assert.Throws<OperationCanceledException>(() =>
            property.Check(new CheckOptions { Seed = 1 }, cancellation.Token));
    }

    [Fact]
    public void Report_WithCustomReplayHint_ShouldCarryIt()
    {
        // Arrange
        var result = Property.ForAll(Generate.Integer<int>(), static x => x < 10).Check(new CheckOptions { Seed = 2 });

        // Act
        var hinted = result.ToString($"[Property(Replay = \"{result.Replay}\")]");
        var exception = Assert.Throws<PropertyFailedException>(() => result.ThrowIfFailed("custom hint"));

        // Assert
        Assert.True(result.IsFalsified);
        Assert.Contains("Replay with: [Property(Replay = \"2:", hinted);
        Assert.Contains("Replay with: new CheckOptions", result.ToString());
        Assert.Contains("Replay with: custom hint", exception.Message);
    }
}
