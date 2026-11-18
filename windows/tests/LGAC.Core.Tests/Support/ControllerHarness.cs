using System.Collections.Concurrent;
using System.Diagnostics;

namespace LGAC.Core.Tests.Support;

internal sealed class CredentialsLog
{
    public List<ThinQCredentials> All { get; } = [];
}

internal sealed class TestNow
{
    public DateTimeOffset Date { get; set; } = DateTimeOffset.FromUnixTimeSeconds(1_000_000);
}

internal static class ControllerHarness
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [ThreadStatic]
    private static RunState? current;

    public static void Run(Func<Task> body)
    {
        var previous = SynchronizationContext.Current;
        var previousRun = current;
        var run = new RunState(Environment.CurrentManagedThreadId);
        current = run;
        var context = new QueueContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var task = body();
            context.Pump(task, Timeout);
            task.GetAwaiter().GetResult();
            if (run.Violations.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Notificação fora da thread do teste ({string.Join(", ", run.Violations)})");
            }
        }
        finally
        {
            current = previousRun;
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    public static async Task YieldAsync()
    {
        await Task.Yield();
    }

    public static async Task ShortDelayAsync(CancellationToken ct)
    {
        await Task.Yield();
        ct.ThrowIfCancellationRequested();
    }

    public static async Task TestDelayAsync(TimeSpan duration, CancellationToken ct)
    {
        if (duration >= ACController.BasePollInterval)
        {
            await Task.Delay(System.Threading.Timeout.InfiniteTimeSpan, ct);
        }

        await ShortDelayAsync(ct);
    }

    public static ACController MakeController(
        FakeClient fake,
        InMemoryCredentialStore? store = null,
        TestNow? clock = null,
        CredentialsLog? log = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var time = clock ?? new TestNow();
        var run = current;
        var controller = new ACController(
            store ?? InMemoryCredentialStore.Configured(),
            credentials =>
            {
                log?.All.Add(credentials);
                return fake;
            },
            delay ?? TestDelayAsync,
            () => time.Date);
        if (run is not null)
        {
            controller.PropertyChanged += (_, args) =>
            {
                if (Environment.CurrentManagedThreadId != run.ThreadId)
                {
                    run.Violations.Add(args.PropertyName ?? "");
                }
            };
        }

        return controller;
    }

    private sealed class RunState(int threadId)
    {
        public int ThreadId { get; } = threadId;

        public List<string> Violations { get; } = [];
    }

    private sealed class QueueContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            queue.Add((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            d(state);
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public void Pump(Task task, TimeSpan limit)
        {
            _ = task.ContinueWith(
                _ => Post(static _ => { }, null),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            var clock = Stopwatch.StartNew();
            while (!task.IsCompleted)
            {
                var remaining = limit - clock.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException($"O teste não terminou em {limit.TotalSeconds:0} s (possível travamento)");
                }

                if (queue.TryTake(out var item, remaining))
                {
                    item.Callback(item.State);
                }
            }
        }
    }
}

internal static class TestDevices
{
    public static readonly ThinQDevice Ac1 = new("ac-1", "DEVICE_AIR_CONDITIONER", "Quarto", "M1");
    public static readonly ThinQDevice Ac2 = new("ac-2", "DEVICE_AIR_CONDITIONER", "Sala", "M2");
    public static readonly ThinQDevice Washer = new("w-1", "DEVICE_WASHER", "Lavadora", "W1");
}
