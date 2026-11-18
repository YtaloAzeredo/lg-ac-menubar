using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LGAC.Core;

public sealed partial class ACController : INotifyPropertyChanged
{
    public static readonly TimeSpan BasePollInterval = TimeSpan.FromSeconds(300);
    public static readonly TimeSpan MaxPollInterval = TimeSpan.FromSeconds(1800);
    internal static readonly TimeSpan Freshness = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan ConfirmDelay = TimeSpan.FromMilliseconds(1500);
    internal static readonly TimeSpan TempDebounce = TimeSpan.FromMilliseconds(600);

    private readonly Func<ThinQCredentials, IThinQClient> makeClient;
    private (IThinQClient Client, string? Token)? pendingSetup;
    private CancellationTokenSource? pollCts;

    private ACPhase phaseValue = new ACPhase.Loading();
    private ACCapabilities? capabilitiesValue;
    private ACState? stateValue;
    private string deviceAliasValue = "";
    private DateTimeOffset? lastUpdatedValue;
    private string? statusMessageValue;
    private bool isDeviceOfflineValue;
    private TimeSpan pollIntervalValue = BasePollInterval;
    private bool isSwitchingDeviceValue;
    private IThinQClient? clientValue;

    public ACController(
        ICredentialStore store,
        Func<ThinQCredentials, IThinQClient>? makeClient = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Func<DateTimeOffset>? now = null)
    {
        Store = store;
        this.makeClient = makeClient ?? (credentials => new ThinQClient(credentials));
        Delay = delay ?? Task.Delay;
        Now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ACPhase Phase
    {
        get => phaseValue;
        internal set => Set(ref phaseValue, value);
    }

    public ACCapabilities? Capabilities
    {
        get => capabilitiesValue;
        internal set => Set(ref capabilitiesValue, value);
    }

    public ACState? State
    {
        get => stateValue;
        internal set => Set(ref stateValue, value);
    }

    public string DeviceAlias
    {
        get => deviceAliasValue;
        internal set => Set(ref deviceAliasValue, value);
    }

    public DateTimeOffset? LastUpdated
    {
        get => lastUpdatedValue;
        internal set => Set(ref lastUpdatedValue, value);
    }

    public string? StatusMessage
    {
        get => statusMessageValue;
        internal set => Set(ref statusMessageValue, value);
    }

    public bool IsDeviceOffline
    {
        get => isDeviceOfflineValue;
        internal set => Set(ref isDeviceOfflineValue, value);
    }

    public TimeSpan PollInterval
    {
        get => pollIntervalValue;
        internal set => Set(ref pollIntervalValue, value);
    }

    public bool IsSwitchingDevice
    {
        get => isSwitchingDeviceValue;
        internal set => Set(ref isSwitchingDeviceValue, value);
    }

    public bool CanSwitchDevice => clientValue is not null;

    internal ICredentialStore Store { get; }

    internal Func<TimeSpan, CancellationToken, Task> Delay { get; }

    internal Func<DateTimeOffset> Now { get; }

    internal IThinQClient? Client
    {
        get => clientValue;
        set
        {
            var could = CanSwitchDevice;
            clientValue = value;
            if (could != CanSwitchDevice)
            {
                Raise(nameof(CanSwitchDevice));
            }
        }
    }

    internal int Session { get; private set; }

    internal Task? PollTask { get; private set; }

    internal Task? ConfirmTask { get; set; }

    internal CancellationTokenSource? ConfirmCts;

    internal Task? TempTask { get; set; }

    internal CancellationTokenSource? TempCts;

    internal double? PendingTargetTemp { get; set; }

    internal ACState? TempBaseline { get; set; }

    public async Task BootstrapAsync()
    {
        string? token;
        try
        {
            token = Store.Token;
        }
        catch (Exception)
        {
            Phase = new ACPhase.NeedsSetup(null);
            return;
        }

        if (token is null || Store.DeviceId is null)
        {
            Phase = new ACPhase.NeedsSetup(null);
            return;
        }

        try
        {
            Client = makeClient(new ThinQCredentials(token, Store.Country, Store.ClientId));
        }
        catch (Exception error)
        {
            Phase = new ACPhase.NeedsSetup(MessageFor(error));
            return;
        }

        DeviceAlias = Store.DeviceAlias ?? "";
        await LoadAsync();
    }

    public async Task ConnectAsync(string token, string country)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0)
        {
            Phase = new ACPhase.NeedsSetup("Cole o token");
            return;
        }

        Phase = new ACPhase.Loading();
        Store.Country = country;
        try
        {
            var newClient = makeClient(new ThinQCredentials(trimmed, country, Store.ClientId));
            var airConditioners = (await newClient.DevicesAsync()).Where(device => device.IsAirConditioner).ToList();
            pendingSetup = (newClient, trimmed);
            switch (airConditioners.Count)
            {
                case 0:
                    Phase = new ACPhase.NeedsSetup("Nenhum ar-condicionado encontrado nesta conta");
                    break;
                case 1:
                    await SelectDeviceAsync(airConditioners[0]);
                    break;
                default:
                    Phase = new ACPhase.ChoosingDevice(airConditioners);
                    break;
            }
        }
        catch (Exception error)
        {
            Phase = new ACPhase.NeedsSetup(MessageFor(error));
        }
    }

    public async Task SelectDeviceAsync(ThinQDevice device)
    {
        if (pendingSetup is not { } setup)
        {
            return;
        }

        if (setup.Token is { } token)
        {
            try
            {
                Store.SaveToken(token);
            }
            catch (Exception)
            {
                Phase = new ACPhase.NeedsSetup("Não foi possível salvar o token");
                return;
            }
        }

        pendingSetup = null;
        IsSwitchingDevice = false;
        Session++;
        ResetDeviceData();
        Store.DeviceId = device.Id;
        Store.DeviceAlias = device.Alias;
        DeviceAlias = device.Alias;
        Client = setup.Client;
        await LoadAsync();
    }

    public async Task ChooseAnotherDeviceAsync()
    {
        if (Client is not { } current)
        {
            Phase = new ACPhase.NeedsSetup(null);
            return;
        }

        var session = Session;
        try
        {
            var airConditioners = (await current.DevicesAsync()).Where(device => device.IsAirConditioner).ToList();
            if (session != Session)
            {
                return;
            }

            if (airConditioners.Count == 0)
            {
                StatusMessage = "Nenhum ar-condicionado encontrado nesta conta";
                return;
            }

            Session++;
            ResetDeviceData();
            Client = null;
            pendingSetup = (current, null);
            IsSwitchingDevice = true;
            Phase = new ACPhase.ChoosingDevice(airConditioners);
        }
        catch (Exception error)
        {
            if (session != Session)
            {
                return;
            }

            Handle(error);
        }
    }

    public async Task CancelDeviceChoiceAsync()
    {
        if (!IsSwitchingDevice || pendingSetup is not { } setup)
        {
            return;
        }

        pendingSetup = null;
        IsSwitchingDevice = false;
        Client = setup.Client;
        await LoadAsync();
    }

    public void ResetCredentials()
    {
        EndSession();
        try
        {
            Store.DeleteToken();
        }
        catch (Exception)
        {
        }

        Phase = new ACPhase.NeedsSetup(null);
    }

    public async Task RefreshAsync(bool force = false)
    {
        await RefreshAsync(force, CancellationToken.None);
    }

    public void StartPolling()
    {
        pollCts?.Cancel();
        pollCts = new CancellationTokenSource();
        PollTask = PollLoopAsync(pollCts.Token);
    }

    public void StopPolling()
    {
        pollCts?.Cancel();
        pollCts = null;
        PollTask = null;
    }

    internal async Task LoadAsync()
    {
        Phase = new ACPhase.Loading();
        Capabilities = null;
        await RefreshAsync(true);
        if (Phase is ACPhase.NeedsSetup)
        {
            return;
        }

        StartPolling();
    }

    internal async Task RefreshAsync(bool force, CancellationToken ct)
    {
        if (Client is not { } current || Store.DeviceId is not { } deviceId)
        {
            return;
        }

        var session = Session;
        if (!force && LastUpdated is { } last && Now() - last < Freshness)
        {
            return;
        }

        try
        {
            if (Capabilities is null)
            {
                var profile = await current.ProfileAsync(deviceId, ct);
                if (session != Session)
                {
                    return;
                }

                Capabilities = new ACCapabilities(profile);
            }

            var fresh = ACState.FromJson(await current.StateAsync(deviceId, ct));
            if (session != Session)
            {
                return;
            }

            if (PendingTargetTemp is { } pending)
            {
                fresh = fresh with { TargetTemp = pending };
            }

            State = fresh;
            LastUpdated = Now();
            StatusMessage = null;
            IsDeviceOffline = false;
            PollInterval = BasePollInterval;
            Phase = new ACPhase.Ready();
        }
        catch (Exception error)
        {
            if (session != Session)
            {
                return;
            }

            Handle(error);
        }
    }

    internal void Handle(Exception error)
    {
        if (error is OperationCanceledException)
        {
            return;
        }

        if (error is not ThinQException thinQ)
        {
            StatusMessage = $"Erro: {error.Message}";
            return;
        }

        switch (thinQ.Kind)
        {
            case ThinQErrorKind.InvalidToken:
                EndSession();
                Phase = new ACPhase.NeedsSetup(thinQ.UserMessage);
                break;
            case ThinQErrorKind.RateLimited:
                PollInterval = TimeSpan.FromTicks(Math.Min(PollInterval.Ticks * 2, MaxPollInterval.Ticks));
                StatusMessage = thinQ.UserMessage;
                break;
            case ThinQErrorKind.DeviceOffline:
                IsDeviceOffline = true;
                StatusMessage = thinQ.UserMessage;
                break;
            default:
                StatusMessage = thinQ.UserMessage;
                break;
        }
    }

    internal static string MessageFor(Exception error)
    {
        return (error as ThinQException)?.UserMessage ?? error.Message;
    }

    internal static void CancelAndClear(ref CancellationTokenSource? source)
    {
        source?.Cancel();
        source = null;
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Delay(PollInterval, ct);
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

    private void EndSession()
    {
        Session++;
        ResetDeviceData();
        Client = null;
        pendingSetup = null;
        IsSwitchingDevice = false;
    }

    private void ResetDeviceData()
    {
        StopPolling();
        CancelAndClear(ref TempCts);
        TempTask = null;
        CancelAndClear(ref ConfirmCts);
        ConfirmTask = null;
        PendingTargetTemp = null;
        TempBaseline = null;
        Capabilities = null;
        State = null;
        LastUpdated = null;
        StatusMessage = null;
        IsDeviceOffline = false;
        PollInterval = BasePollInterval;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Raise(name);
    }

    private void Raise(string? name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
