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
}
