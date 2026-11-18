using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using LGAC.Core;
using LGAC.Linux.Platform;
using LGAC.Linux.Tray;
using LGAC.Linux.Views;

namespace LGAC.Linux;

public partial class App : Application
{
    private static readonly TimeSpan TrayProbeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TrayWaitLimit = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan TrayWaitInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SignalTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SmokeSettle = TimeSpan.FromMilliseconds(300);

    private AppComposition? composition;
    private readonly ActivationRelay activation = new();

    internal static LaunchMode? RealLaunch { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (RealLaunch is { } mode && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Dispatcher.UIThread.Post(() => _ = mode == LaunchMode.Smoke ? RunSmokeAsync(desktop) : RunAsync(desktop));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task ObserveAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception)
        {
        }
    }

    private static void RepairAutostart(AutostartEntry autostart, string? processPath)
    {
        if (string.IsNullOrEmpty(processPath))
        {
            return;
        }

        try
        {
            autostart.RepairPath();
        }
        catch (Exception)
        {
        }
    }

    private static async Task<bool> TrayAvailableAsync()
    {
        try
        {
            return await TrayAvailability.WaitAsync(
                () => TrayAvailability.IsAvailableAsync(TrayProbeTimeout),
                TrayAvailability.ExpectsTray(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")),
                TrayWaitLimit,
                TrayWaitInterval);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void OnActivateRequested()
    {
        Dispatcher.UIThread.Post(activation.Request);
    }

    private SingleInstance? TryAcquireInstance(XdgPaths paths, out bool otherRunning)
    {
        otherRunning = false;
        try
        {
            if (!paths.EnsurePrivateRuntimeDir())
            {
                return null;
            }

            var instance = SingleInstance.TryAcquire(paths.LockFile, paths.SocketFile, OnActivateRequested);
            otherRunning = instance is null;
            return instance;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        SingleInstance? instance = null;
        try
        {
            var paths = XdgPaths.FromEnvironment();
            instance = TryAcquireInstance(paths, out var otherRunning);
            if (otherRunning && SingleInstance.SignalExisting(paths.SocketFile, SignalTimeout))
            {
                desktop.Shutdown(0);
                return;
            }

            var vault = new FallbackTokenVault(
                new DBusSecretService(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(60)),
                new FileTokenVault(paths.TokenFile));
            var store = new PrimedCredentialStore(new FileCredentialStore(paths.SettingsFile, vault));
            var processPath = Environment.ProcessPath;
            var autostart = new AutostartEntry(paths.AutostartFile, processPath ?? string.Empty);
            RepairAutostart(autostart, processPath);

            var controller = new ACController(store);
            var trayAvailable = await TrayAvailableAsync();

            var created = new AppComposition(
                controller,
                store,
                autostart,
                trayAvailable,
                (toggle, show, quit) => new TrayPresenter(controller, toggle, show, quit),
                () => desktop.Shutdown(0));
            if (instance is not null)
            {
                created.Own(instance);
                instance = null;
            }

            composition = created;
            created.Start();
            activation.Attach(created.Activate);

            _ = ObserveAsync(() => created.BootstrapAsync(store.Prime));

            var watcher = new ResumeWatcher(created.OnResume);
            created.Own(watcher);
            _ = ObserveAsync(watcher.Start);
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("lg-ac: falha ao iniciar (" + error.GetType().Name + ")");
            instance?.Dispose();
            composition?.Quit();
            desktop.Shutdown(1);
        }
    }

    private static async Task RunExtraSmokeChecksAsync(ACController controller, ICredentialStore store, AutostartEntry autostart)
    {
        await TrayAvailability.IsAvailableAsync(TrayProbeTimeout);
        await Task.Run(() => new DBusSecretService(TrayProbeTimeout, TrayProbeTimeout).IsAvailable());

        var digest = SHA256.HashData([1, 2, 3, 4]);
        if (digest.Length != 32)
        {
            throw new InvalidOperationException("a criptografia do sistema devolveu um resultado inesperado");
        }

        var services = new PanelServices(controller, store, autostart, () => { });
        var size = new Size(400, 600);
        IReadOnlyList<ThinQDevice> devices = [new ThinQDevice(SmokeClient.DeviceId, "DEVICE_AIR_CONDITIONER", "Ar de teste", "SMOKE")];
        using var setup = new SetupView(services, null, _ => Task.CompletedTask);
        using var choice = new DeviceChoiceView(services, devices);
        using var loading = new LoadingView(services);
        foreach (var view in new Control[] { setup, choice, loading })
        {
            view.Measure(size);
            view.Arrange(new Rect(size));
        }

        _ = new ThinQClient(new ThinQCredentials("smoke", "BR", "lgac-smoke"));

        ModeIcon.Render("COOL", false, 44);
        var dimmedIcon = ModeIcon.Render(null, true, 22);
        var application = Application.Current ?? throw new InvalidOperationException("o aplicativo não foi inicializado");
        var trayIcon = new TrayIcon
        {
            Icon = dimmedIcon,
            Menu = new NativeMenu { new NativeMenuItem("Abrir painel") },
            ToolTipText = "LG AC",
            IsVisible = false
        };
        try
        {
            TrayIcon.SetIcons(application, new TrayIcons { trayIcon });
        }
        finally
        {
            TrayIcon.SetIcons(application, new TrayIcons());
            trayIcon.Dispose();
        }
    }

    private async Task RunSmokeAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var exitCode = 0;
        try
        {
            var store = new SmokeCredentialStore();
            var controller = new ACController(store, _ => new SmokeClient());
            var autostart = new AutostartEntry(Path.Combine(Path.GetTempPath(), "lg-ac-smoke", "lg-ac.desktop"), "lg-ac");
            var created = new AppComposition(
                controller,
                store,
                autostart,
                false,
                (_, _, _) => throw new InvalidOperationException("bandeja indisponível no smoke"),
                () => desktop.Shutdown(exitCode));
            composition = created;
            var panel = created.Start();

            await controller.BootstrapAsync();
            if (controller.Phase is not ACPhase.Ready)
            {
                throw new InvalidOperationException("o controlador não chegou a Ready");
            }

            panel.UpdateLayout();
            if (panel.CurrentView is not ControlPanelView)
            {
                throw new InvalidOperationException("o painel não mostrou a tela de controle");
            }

            await RunExtraSmokeChecksAsync(controller, store, autostart);

            await Task.Delay(SmokeSettle);
        }
        catch (Exception error)
        {
            exitCode = 1;
            Console.Error.WriteLine("lg-ac --smoke: " + error.Message);
        }
        finally
        {
            composition?.Quit();
            if (exitCode != 0)
            {
                desktop.Shutdown(exitCode);
            }
        }
    }
}
