using System.Text.Json.Nodes;

namespace LGAC.Core.Tests.Support;

internal sealed class FakeClient : IThinQClient
{
    private IReadOnlyList<ThinQDevice> devices;
    private Exception? devicesError;
    private JsonNode? profile;
    private Exception? profileError;
    private JsonNode? state;
    private Exception? stateError;
    private Exception? controlError;
    private bool holdState;
    private TaskCompletionSource? stateGate;
    private bool holdControl;
    private TaskCompletionSource? controlGate;

    public FakeClient(JsonNode? profile, JsonNode? state, IReadOnlyList<ThinQDevice>? devices = null)
    {
        this.devices = devices ?? [];
        this.profile = profile;
        this.state = state;
    }

    public List<JsonNode> Controls { get; } = [];

    public int StateCalls { get; private set; }

    public bool IsStateHeld { get; private set; }

    public bool IsControlHeld { get; private set; }

    public static FakeClient Full()
    {
        return new FakeClient(Fixture.Json("profile-full"), Fixture.Json("state-full"));
    }

    public Task<IReadOnlyList<ThinQDevice>> DevicesAsync(CancellationToken ct = default)
    {
        return devicesError is null ? Task.FromResult(devices) : Task.FromException<IReadOnlyList<ThinQDevice>>(devicesError);
    }

    public Task<JsonNode?> ProfileAsync(string deviceId, CancellationToken ct = default)
    {
        return profileError is null ? Task.FromResult(profile) : Task.FromException<JsonNode?>(profileError);
    }

    public async Task<JsonNode?> StateAsync(string deviceId, CancellationToken ct = default)
    {
        StateCalls++;
        if (holdState)
        {
            IsStateHeld = true;
            stateGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await stateGate.Task;
        }

        return stateError is null ? state : throw stateError;
    }

    public async Task ControlAsync(string deviceId, JsonNode payload, CancellationToken ct = default)
    {
        Controls.Add(payload);
        if (holdControl)
        {
            IsControlHeld = true;
            controlGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await controlGate.Task;
        }

        if (controlError is not null)
        {
            throw controlError;
        }
    }

    public void HoldNextState()
    {
        holdState = true;
    }

    public void ReleaseState()
    {
        holdState = false;
        IsStateHeld = false;
        stateGate?.SetResult();
        stateGate = null;
    }

    public void HoldNextControl()
    {
        holdControl = true;
    }

    public void ReleaseControl()
    {
        holdControl = false;
        IsControlHeld = false;
        controlGate?.SetResult();
        controlGate = null;
    }

    public void SetDevices(IReadOnlyList<ThinQDevice> value)
    {
        devices = value;
        devicesError = null;
    }

    public void FailDevices(Exception error)
    {
        devicesError = error;
    }

    public void SetProfile(JsonNode? value)
    {
        profile = value;
        profileError = null;
    }

    public void FailProfile(Exception error)
    {
        profileError = error;
    }

    public void SetState(JsonNode? value)
    {
        state = value;
        stateError = null;
    }

    public void FailState(Exception error)
    {
        stateError = error;
    }

    public void SetControlError(Exception? error)
    {
        controlError = error;
    }
}
