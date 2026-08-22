using System.Runtime.InteropServices;
using Xunit;

namespace QuickCheck.Xunit.Tests;

/// <summary>
/// The samples in src/QuickCheck.Xunit.v3/readme.md
/// </summary>
public sealed class ReadmeTests
{
    private static readonly int[] s_empty = [0, 0, 0];

    public sealed record Request(string Path, int Attempt);

    public sealed record Transfer(int From, int To, int Amount);

    public static class Generators
    {
        public static Generator<int> Small => Generate.Between(-10, 10);
    }

    private static Generator<int> Small { get; } = Generate.Between(-10, 10);


    [Property]
    public void Round_trips(string s) => Assert.Equal(s, Decode(Encode(s)));

    [Property(RunCount = 500)]
    public bool Sorting_is_idempotent(List<int> items) =>
        items.Order().SequenceEqual(items.Order().Order());

    [Property]
    public async Task Handles_any_request(Request request) => await Handle(request);

    [Property]
    public void Deposits_commute([Generator(nameof(Small))] int a, [Generator(nameof(Small))] int b) =>
        Assert.Equal(Deposit(Deposit(s_empty, a), b), Deposit(Deposit(s_empty, b), a));

    [Property(Generators = typeof(Generators))]
    public void Transfers_preserve_total(Transfer transfer)
    {
        Assert.InRange(transfer.Amount, -10, 10);
        Assert.Equal(Total(Apply(transfer)), Total(s_empty));
    }

    // Base64 over the raw UTF-16 code units rather than UTF-8: Generate.Char() produces lone
    // surrogates, which a UTF-8 round trip replaces with U+FFFD.
    private static string Encode(string s) => Convert.ToBase64String(MemoryMarshal.AsBytes(s.AsSpan()));

    private static string Decode(string s) => new(MemoryMarshal.Cast<byte, char>(Convert.FromBase64String(s)));

    private static async Task Handle(Request request)
    {
        await Task.Yield();
        _ = request.Path.Length + request.Attempt;
    }

    private static int[] Deposit(int[] accounts, int amount)
    {
        var result = (int[])accounts.Clone();
        result[0] += amount;
        return result;
    }

    private static int[] Apply(Transfer transfer)
    {
        var result = (int[])s_empty.Clone();
        var from = Math.Abs(transfer.From % result.Length);
        var to = Math.Abs(transfer.To % result.Length);
        result[from] -= transfer.Amount;
        result[to] += transfer.Amount;
        return result;
    }

    private static int Total(int[] accounts) => accounts.Sum();
}
