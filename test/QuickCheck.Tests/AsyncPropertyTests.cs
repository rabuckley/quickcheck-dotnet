namespace QuickCheck.Tests;

public sealed class AsyncPropertyTests
{
    [Fact]
    public async Task Async_body_is_awaited_for_every_example()
    {
        var seen = 0;

        var result = await Property
            .ForAll(Generate.Integer<int>(), async x =>
            {
                await Task.Yield();
                Interlocked.Increment(ref seen);
                _ = x;
            })
            .CheckAsync(new CheckOptions { RunCount = 30 });

        Assert.Equal(PropertyOutcome.Passed, result.Outcome);
        Assert.Equal(30, seen);
    }

    /// <summary>
    /// A throwing body is the case that tells an awaited body from one bound as <c>async void</c>:
    /// the exception of an unawaited body is never seen, so the property would pass.
    /// </summary>
    [Fact]
    public async Task Async_body_that_throws_falsifies_the_property()
    {
        var result = await Property
            .ForAll(Generate.Integer<int>(), async x =>
            {
                await Task.Yield();

                if (x > 10)
                {
                    throw new InvalidOperationException("too big");
                }
            })
            .CheckAsync(new CheckOptions { Seed = 1, RunCount = 1000 });

        Assert.True(result.IsFalsified);
        Assert.Equal(11, result.Minimal.Value);
    }

    [Fact]
    public async Task Exception_from_an_async_body_is_reported_unwrapped()
    {
        var result = await Property
            .ForAll(Generate.Integer<int>(), Generate.Integer<int>(), async (a, b) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("thrown from the body");
            })
            .CheckAsync(new CheckOptions { RunCount = 1 });

        Assert.True(result.IsFalsified);
        var exception = Assert.IsType<InvalidOperationException>(result.Minimal.Exception);
        Assert.Equal("thrown from the body", exception.Message);
    }

    [Fact]
    public async Task Async_failures_are_shrunk_like_synchronous_ones()
    {
        var property = Property.ForAll(Generate.Integer<int>().List(), async items =>
        {
            await Task.Yield();
            return items.Sum(static x => (long)x) < 100;
        });

        var result = await property.CheckAsync(new CheckOptions { Seed = 7, RunCount = 500 });

        Assert.True(result.IsFalsified);
        Assert.Equal([100], result.Minimal.Value);

        var exception = await Assert.ThrowsAsync<PropertyFailedException>(
            async () => await property.AssertAsync(new CheckOptions { Seed = 7, RunCount = 500 }));
        Assert.Contains("Minimal counterexample: [100]", exception.Message);
    }

    /// <summary>
    /// Every asynchronous <c>ForAll</c> overload, so that an arity or body shape cannot be dropped
    /// or shadowed by the synchronous overloads without a test failing to compile.
    /// </summary>
    [Fact]
    public async Task Every_asynchronous_overload_is_accepted()
    {
        var integers = Generate.Integer<int>();
        var booleans = Generate.Boolean();

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

        Assert.All(outcomes, static outcome => Assert.Equal(PropertyOutcome.Passed, outcome));
    }

    [Fact]
    public void Cancellation_aborts_the_check_between_examples()
    {
        using var cancellation = new CancellationTokenSource();
        var runs = 0;

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            if (++runs == 5)
            {
                cancellation.Cancel();
            }
        });

        Assert.Throws<OperationCanceledException>(() =>
            property.Check(new CheckOptions { RunCount = 1000 }, cancellation.Token));
        Assert.Equal(5, runs);
    }

    [Fact]
    public async Task Cancellation_aborts_an_async_check_between_examples()
    {
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

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => property.CheckAsync(new CheckOptions { RunCount = 1000 }, cancellation.Token));
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
    public void Cancelling_from_the_body_aborts_the_check(int maxShrinkAttempts)
    {
        using var cancellation = new CancellationTokenSource();
        var abandoned = new OperationCanceledException("abandoned", cancellation.Token);

        var property = Property.ForAll(Generate.Integer<int>(), void (_) =>
        {
            cancellation.Cancel();
            throw abandoned;
        });

        var thrown = Assert.ThrowsAny<OperationCanceledException>(() =>
            property.Check(new CheckOptions { MaxShrinkAttempts = maxShrinkAttempts }, cancellation.Token));

        Assert.Same(abandoned, thrown);
    }

    [Fact]
    public async Task Cancelling_from_an_async_body_aborts_the_check()
    {
        using var cancellation = new CancellationTokenSource();

        var property = Property.ForAll(Generate.Integer<int>(), async _ =>
        {
            await Task.Yield();
            await cancellation.CancelAsync();
            cancellation.Token.ThrowIfCancellationRequested();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => property.CheckAsync(new CheckOptions { MaxShrinkAttempts = 0 }, cancellation.Token));
    }

    /// <summary>
    /// Cancellation requested by the failing example itself is observed on the way into shrinking,
    /// where there may be no attempt left to observe it.
    /// </summary>
    [Fact]
    public void Cancellation_requested_by_the_failing_example_aborts_rather_than_reporting()
    {
        using var cancellation = new CancellationTokenSource();

        var property = Property.ForAll(Generate.Integer<int>(), _ =>
        {
            cancellation.Cancel();
            return false;
        });

        Assert.Throws<OperationCanceledException>(() =>
            property.Check(new CheckOptions { MaxShrinkAttempts = 0 }, cancellation.Token));
    }

    [Fact]
    public void Cancellation_during_shrinking_propagates()
    {
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

        Assert.Throws<OperationCanceledException>(() =>
            property.Check(new CheckOptions { Seed = 1 }, cancellation.Token));
    }
}
