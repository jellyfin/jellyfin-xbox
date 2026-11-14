using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Core
{
    /// <summary>
    /// A lightweight async auto-reset event. Each <see cref="Set"/> releases exactly one waiter,
    /// or, if nobody is waiting, lets the next <see cref="WaitAsync(CancellationToken)"/> complete immediately.
    /// </summary>
    public sealed class AsyncAutoResetEventSlim
    {
        private static readonly Task CompletedTask = Task.CompletedTask;

        private readonly object _lock = new object();
        private readonly LinkedList<TaskCompletionSource<bool>> _waiters = new LinkedList<TaskCompletionSource<bool>>();
        private bool _signaled;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAutoResetEventSlim"/> class.
        /// </summary>
        /// <param name="initialState">Whether the event starts in the signaled state.</param>
        public AsyncAutoResetEventSlim(bool initialState = false)
        {
            _signaled = initialState;
        }

        /// <summary>
        /// Signals the event. Releases one waiter, or stores the signal if none is waiting.
        /// </summary>
        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;
            lock (_lock)
            {
                if (_waiters.Count > 0)
                {
                    toRelease = _waiters.First.Value;
                    _waiters.RemoveFirst();
                }
                else
                {
                    _signaled = true;
                }
            }

            toRelease?.TrySetResult(true);
        }

        /// <summary>
        /// Waits asynchronously until the event is signaled.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the wait.</param>
        /// <returns>A task that completes when the event is signaled.</returns>
        public Task WaitAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            LinkedListNode<TaskCompletionSource<bool>> node;
            lock (_lock)
            {
                if (_signaled)
                {
                    _signaled = false;
                    return CompletedTask;
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                node = _waiters.AddLast(tcs);
            }

            if (!cancellationToken.CanBeCanceled)
            {
                return node.Value.Task;
            }

            var registration = cancellationToken.Register(
                () =>
                {
                    lock (_lock)
                    {
                        if (node.List != null)
                        {
                            _waiters.Remove(node);
                        }
                    }

                    node.Value.TrySetCanceled(cancellationToken);
                });

            return node.Value.Task.ContinueWith(
                (t, state) =>
                {
                    ((IDisposable)state).Dispose();
                    return t;
                },
                registration,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default).Unwrap();
        }
    }
}
