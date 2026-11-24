using FileSort.Engine;

namespace FileSort.Tests.Engine;

/// <summary>The items of a step on several threads: every item runs once, and a failure leaves as it was
/// thrown.</summary>
public class ParallelWorkTests
{
    // An item that fails.
    private static void Fail(int item)
    {
        throw new InvalidOperationException($"Item {item} failed.");
    }

    [Fact]
    public void Every_item_runs_once()
    {
        int[] runs = new int[100];

        ParallelWork work = new(4, TestContext.Current.CancellationToken);
        work.For(runs.Length, item => Interlocked.Increment(ref runs[item]));

        Assert.All(runs, count => Assert.Equal(1, count));
    }

    [Fact]
    public void A_failure_of_an_item_leaves_as_it_was_thrown()
    {
        ParallelWork work = new(4, TestContext.Current.CancellationToken);

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => work.For(8, Fail));

        Assert.StartsWith("Item ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failure_wins_over_a_cancellation_that_it_caused()
    {
        ParallelWork work = new(2, TestContext.Current.CancellationToken);
        using Barrier both = new(2);

        // Both items run at once: one fails, the other stops as a cancelled item would.
        Action<int> body = item =>
        {
            both.SignalAndWait(TestContext.Current.CancellationToken);
            throw item == 0 ? new OperationCanceledException() : new IOException("The disk is full.");
        };

        Assert.Throws<IOException>(() => work.For(2, body));
    }

    [Fact]
    public void A_cancelled_step_runs_nothing()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        int runs = 0;

        ParallelWork work = new(4, cancellation.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => work.For(10, _ => Interlocked.Increment(ref runs)));

        Assert.Equal(0, runs);
    }
}
