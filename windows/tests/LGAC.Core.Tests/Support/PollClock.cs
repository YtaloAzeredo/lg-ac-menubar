namespace LGAC.Core.Tests.Support;

internal sealed class PollClock
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    private TaskCompletionSource? waiting;

    public List<TimeSpan> Intervals { get; } = [];

    public async Task SleepAsync(TimeSpan duration, CancellationToken ct)
    {
        if (duration < ACController.BasePollInterval)
        {
            await ControllerHarness.ShortDelayAsync(ct);
            return;
        }

        ct.ThrowIfCancellationRequested();
        Intervals.Add(duration);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting = gate;
        using var registration = ct.Register(() => gate.TrySetCanceled(ct));
        await gate.Task;
    }

    public void Tick()
    {
        waiting?.TrySetResult();
        waiting = null;
    }

    public async Task WaitForSleepsAsync(int count)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (Intervals.Count < count && started.Elapsed < Deadline)
        {
            await Task.Yield();
        }

        Assert.IsGreaterThanOrEqualTo(count, Intervals.Count, "o polling não voltou a esperar");
    }
}
