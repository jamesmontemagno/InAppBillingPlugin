using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Plugin.InAppBilling
{
    internal sealed class StoreKit2EventDispatcher
    {
        readonly object gate = new();
        Listener current;
        nuint generation;

        sealed class Listener
        {
            public nuint Context { get; init; }
            public Func<StoreKitEvent, CancellationToken, Task> Handler { get; init; }
            public CancellationTokenSource Cancellation { get; } = new();
            public Queue<StoreKitEvent> Pending { get; } = new();
            public bool Processing { get; set; }
        }

        public (nuint Context, CancellationToken Token) Start(Func<StoreKitEvent, CancellationToken, Task> handler, Action<nuint> start)
        {
            ArgumentNullException.ThrowIfNull(handler);
            lock (gate)
            {
                if (current != null)
                    throw new InvalidOperationException("Only one StoreKit 2 billing instance can be active. Dispose the existing instance before creating another.");

                var listener = new Listener { Context = checked(++generation), Handler = handler };
                current = listener;
                try
                {
                    start(listener.Context);
                    return (listener.Context, listener.Cancellation.Token);
                }
                catch
                {
                    current = null;
                    listener.Cancellation.Cancel();
                    if (!listener.Processing)
                        listener.Cancellation.Dispose();
                    throw;
                }
            }
        }

        public void Stop(nuint context, Action stop)
        {
            lock (gate)
            {
                if (current == null || current.Context != context)
                    return;

                var listener = current;
                current = null;
                listener.Pending.Clear();
                listener.Cancellation.Cancel();
                try
                {
                    stop();
                }
                finally
                {
                    if (!listener.Processing)
                        listener.Cancellation.Dispose();
                }
            }
        }

        public void Enqueue(nuint context, StoreKitEvent storeEvent)
        {
            lock (gate)
            {
                if (current == null || current.Context != context)
                    return;

                var listener = current;
                listener.Pending.Enqueue(storeEvent);
                if (!listener.Processing)
                {
                    listener.Processing = true;
                    _ = Task.Run(() => ProcessAsync(listener));
                }
            }
        }

        async Task ProcessAsync(Listener listener)
        {
            var cancellationToken = listener.Cancellation.Token;
            while (true)
            {
                StoreKitEvent storeEvent;
                lock (gate)
                {
                    if (current != listener || listener.Pending.Count == 0)
                    {
                        listener.Processing = false;
                        if (current != listener)
                            listener.Cancellation.Dispose();
                        return;
                    }
                    storeEvent = listener.Pending.Dequeue();
                }

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await listener.Handler(storeEvent, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Unable to process StoreKit event: " + ex);
                }
            }
        }
    }
}
