using System.Text.Json.Nodes;

namespace LGAC.Core;

public sealed partial class ACController
{
    public async Task SendAsync(ACCommand command)
    {
        if (Capabilities is not { } caps || State is not { } current || command.Payload(caps) is not { } payload)
        {
            return;
        }

        State = command.Apply(current);
        await DeliverAsync(payload, current);
    }

    public void StepTargetTemp(int steps)
    {
        if (Capabilities?.TargetTemp is not { } temp || State is not { } current
            || (PendingTargetTemp ?? current.TargetTemp) is not { } value)
        {
            return;
        }

        var next = temp.Clamp(value + steps * temp.Step);
        if (next == value)
        {
            return;
        }

        TempBaseline ??= current;
        PendingTargetTemp = next;
        State = new ACCommand.TargetTemp(next).Apply(current);
        CancelAndClear(ref TempCts);
        TempCts = new CancellationTokenSource();
        TempTask = DebounceAsync(TempCts.Token);
    }

    internal async Task FlushTargetTempAsync()
    {
        if (PendingTargetTemp is not { } target || TempBaseline is not { } baseline)
        {
            return;
        }

        PendingTargetTemp = null;
        TempBaseline = null;
        if (Capabilities is not { } caps || new ACCommand.TargetTemp(target).Payload(caps) is not { } payload)
        {
            return;
        }

        await DeliverAsync(payload, baseline);
    }

    internal async Task DeliverAsync(JsonNode payload, ACState previous)
    {
        if (Client is not { } current || Store.DeviceId is not { } deviceId)
        {
            return;
        }

        var session = Session;
        try
        {
            await current.ControlAsync(deviceId, payload);
            if (session != Session)
            {
                return;
            }

            StatusMessage = null;
            ScheduleConfirmation();
        }
        catch (Exception error)
        {
            if (session != Session)
            {
                return;
            }

            if (error is not OperationCanceledException)
            {
                State = previous;
            }

            Handle(error);
        }
    }

    internal void ScheduleConfirmation()
    {
        CancelAndClear(ref ConfirmCts);
        ConfirmCts = new CancellationTokenSource();
        ConfirmTask = ConfirmAsync(ConfirmCts.Token);
    }

    private async Task DebounceAsync(CancellationToken ct)
    {
        try
        {
            await Delay(TempDebounce, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        await FlushTargetTempAsync();
    }

    private async Task ConfirmAsync(CancellationToken ct)
    {
        try
        {
            await Delay(ConfirmDelay, ct);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        await RefreshAsync(true, ct);
    }
}
