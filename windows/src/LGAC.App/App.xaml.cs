using System.IO;
using System.Security.Principal;
using System.Windows;
using LGAC.App.Platform;
using LGAC.App.Theme;
using LGAC.App.Tray;
using LGAC.App.Views;
using LGAC.Core;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace LGAC.App;

public partial class App : Application
{
    private const string MutexNamePrefix = @"Local\LGAC.App.SingleInstance";
    private static readonly TimeSpan ResumeDelay = TimeSpan.FromSeconds(5);

    private Mutex? instanceMutex;
    private bool ownsMutex;
    private CancellationTokenSource? lifetime;
    private bool powerHandlerAttached;

    public new static App Current => (App)Application.Current;

    public ACController Controller { get; private set; } = null!;

    public ICredentialStore Store { get; private set; } = null!;

    public StartupRegistration StartupRegistration { get; private set; } = null!;

    public TrayIcon Tray { get; private set; } = null!;

    public Action<Rect>? TrayToggle { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryAcquireInstance())
        {
            Shutdown();
            return;
        }

        ownsMutex = true;
        lifetime = new CancellationTokenSource();

        var settingsPath = Path.Combine(SettingsRoot(), "LG AC", "settings.json");
        Store = new FileCredentialStore(settingsPath, new WindowsCredentialVault());

        StartupRegistration = new StartupRegistration(new RegistryRunKey(), Environment.ProcessPath ?? string.Empty);
        if (!string.IsNullOrEmpty(Environment.ProcessPath))
        {
            try
            {
                StartupRegistration.RepairPath();
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
            }
        }

        Controller = new ACController(Store);

        SystemTheme.Start();
        var panel = new PanelWindow(Controller, Store, StartupRegistration);
        TrayToggle = _ => panel.Toggle(Tray.LastAnchor);
        Tray = new TrayIcon(Controller, anchor => TrayToggle?.Invoke(anchor));

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        powerHandlerAttached = true;

        _ = ObserveAsync(Controller.BootstrapAsync);
    }

    private bool TryAcquireInstance()
    {
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        try
        {
            var mutex = new Mutex(true, MutexNamePrefix + "." + sid, out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                return false;
            }

            instanceMutex = mutex;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string SettingsRoot()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create);
        if (!string.IsNullOrWhiteSpace(roaming))
        {
            return roaming;
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);
        return string.IsNullOrWhiteSpace(local) ? Path.GetTempPath() : local;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (powerHandlerAttached)
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            powerHandlerAttached = false;
        }

        lifetime?.Cancel();
        SystemTheme.Stop();
        Tray?.Dispose();
        lifetime?.Dispose();
        lifetime = null;

        if (instanceMutex is not null)
        {
            if (ownsMutex)
            {
                instanceMutex.ReleaseMutex();
            }

            instanceMutex.Dispose();
            instanceMutex = null;
        }

        base.OnExit(e);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        Dispatcher.BeginInvoke(() => _ = ObserveAsync(RefreshAfterResumeAsync));
    }

    private async Task RefreshAfterResumeAsync()
    {
        var token = lifetime?.Token ?? CancellationToken.None;
        await Task.Delay(ResumeDelay, token);
        await Controller.RefreshAsync(force: true);
    }

    private static async Task ObserveAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
    }
}
