using Xunit;

namespace Plugin.InAppBilling.Tests;

public sealed class StoreKitInteropTests
{
    [Fact]
    public async Task Null_purchase_is_rejected_before_loading_the_native_library()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => StoreKit2Native.PurchaseAsync(null, null));
    }

    [Fact]
    public async Task Already_canceled_purchase_is_not_dispatched_to_native_code()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            StoreKit2Native.PurchaseAsync("product", null, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task Transaction_events_await_the_previous_handler_before_processing_revocation()
    {
        var dispatcher = new StoreKit2EventDispatcher();
        var entered = Completion();
        var release = Completion();
        var done = Completion();
        var handled = new List<string>();
        var listener = dispatcher.Start(async (storeEvent, token) =>
        {
            if (storeEvent.Kind == "purchase")
            {
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }
            handled.Add(storeEvent.Kind);
            if (storeEvent.Kind == "revocation")
                done.SetResult();
        }, _ => { });

        try
        {
            dispatcher.Enqueue(listener.Context, new StoreKitEvent { Kind = "purchase" });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            dispatcher.Enqueue(listener.Context, new StoreKitEvent { Kind = "revocation" });
            release.SetResult();
            await done.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(["purchase", "revocation"], handled);
        }
        finally
        {
            dispatcher.Stop(listener.Context, () => { });
        }
    }

    [Fact]
    public async Task Stop_discards_queued_and_stale_events_without_stopping_the_replacement_listener()
    {
        var dispatcher = new StoreKit2EventDispatcher();
        var entered = Completion();
        var release = Completion();
        var stopped = Completion();
        var oldCalls = 0;
        var stops = 0;
        var old = dispatcher.Start(async (_, token) =>
        {
            Interlocked.Increment(ref oldCalls);
            entered.SetResult();
            await release.Task;
            Assert.True(token.IsCancellationRequested);
            stopped.SetResult();
        }, _ => { });

        Assert.Throws<InvalidOperationException>(() =>
            dispatcher.Start((_, _) => Task.CompletedTask, _ => { }));

        dispatcher.Enqueue(old.Context, new StoreKitEvent());
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dispatcher.Enqueue(old.Context, new StoreKitEvent());
        dispatcher.Stop(old.Context, () => stops++);
        Assert.True(old.Token.IsCancellationRequested);

        var delivered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var current = dispatcher.Start((storeEvent, _) =>
        {
            delivered.TrySetResult(storeEvent.Kind);
            return Task.CompletedTask;
        }, _ => { });

        try
        {
            Assert.NotEqual(old.Context, current.Context);
            dispatcher.Stop(old.Context, () => stops++);
            dispatcher.Enqueue(old.Context, new StoreKitEvent { Kind = "stale" });
            dispatcher.Enqueue(current.Context, new StoreKitEvent { Kind = "fresh" });
            release.SetResult();

            Assert.Equal("fresh", await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, oldCalls);
            Assert.Equal(1, stops);
            Assert.False(current.Token.IsCancellationRequested);
        }
        finally
        {
            release.TrySetResult();
            dispatcher.Stop(current.Context, () => stops++);
        }
    }

    [Fact]
    public void Failed_native_listener_start_does_not_keep_ownership()
    {
        var dispatcher = new StoreKit2EventDispatcher();
        Assert.Throws<NotSupportedException>(() =>
            dispatcher.Start((_, _) => Task.CompletedTask, _ => throw new NotSupportedException()));

        var listener = dispatcher.Start((_, _) => Task.CompletedTask, _ => { });
        dispatcher.Stop(listener.Context, () => { });
        Assert.True(listener.Token.IsCancellationRequested);
    }

    static TaskCompletionSource Completion() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
