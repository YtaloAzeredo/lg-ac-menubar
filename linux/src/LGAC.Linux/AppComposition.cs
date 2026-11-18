using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using LGAC.Core;
using LGAC.Linux.Platform;
using LGAC.Linux.Tray;
using LGAC.Linux.Views;

namespace LGAC.Linux;

internal sealed class AppComposition
{
    internal static readonly TimeSpan DefaultResumeDelay = TimeSpan.FromSeconds(5);
    private const double FallbackHeightDips = 520;

    private readonly ACController controller;
    private readonly ICredentialStore store;
    private readonly AutostartEntry autostart;
    private readonly bool trayAvailable;
    private readonly Func<Action, Action, Action, IDisposable> createTray;
    private readonly Action shutdown;
    private readonly TimeSpan resumeDelay;
    private readonly Func<ScreenInfo?>? screenProvider;
    private readonly CancellationTokenSource lifetime = new();
    private readonly List<IDisposable> owned = [];
    private CancellationTokenSource? resumeCts;
    private Size? lastPanelSize;
    private IDisposable? tray;
    private PanelWindow? panel;
    private bool trayMode;
    private bool quit;

    public AppComposition(
        ACController controller,
        ICredentialStore store,
        AutostartEntry autostart,
        bool trayAvailable,
        Func<Action, Action, Action, IDisposable> createTray,
        Action shutdown,
        TimeSpan? resumeDelay = null,
        Func<ScreenInfo?>? screenProvider = null)
    {
        this.controller = controller;
        this.store = store;
        this.autostart = autostart;
        this.trayAvailable = trayAvailable;
        this.createTray = createTray;
        this.shutdown = shutdown;
        this.resumeDelay = resumeDelay ?? DefaultResumeDelay;
        this.screenProvider = screenProvider;
    }

    public PanelWindow? Panel => panel;

    public PanelWindow Start()
    {
        if (panel is not null)
        {
            return panel;
        }

        trayMode = trayAvailable && TryCreateTray();
        var created = new PanelWindow(new PanelServices(controller, store, autostart, Quit), trayMode);
        panel = created;
        if (trayMode)
        {
            created.PropertyChanged += OnPanelPropertyChanged;
            return created;
        }

        created.Icon = AppIcon.Load();
        created.ShowPanel();
        return created;
    }

    public void Own(IDisposable disposable)
    {
        if (quit)
        {
            disposable.Dispose();
            return;
        }

        owned.Add(disposable);
    }

    public void Activate()
    {
        Guard(() =>
        {
            if (quit || panel is null)
            {
                return;
            }

            Guard(PositionPanel);
            panel.ShowPanel();
            SchedulePositionCorrection();
        });
    }

    public void Toggle()
    {
        Guard(() =>
        {
            if (quit || panel is null)
            {
                return;
            }

            if (trayMode && !panel.IsVisible)
            {
                Guard(PositionPanel);
            }

            panel.Toggle();
            if (panel.IsVisible)
            {
                SchedulePositionCorrection();
            }
        });
    }

    public void OnResume()
    {
        Dispatcher.UIThread.Post(() => _ = ObserveAsync(RefreshAfterResumeAsync));
    }

    public async Task RefreshAfterResumeAsync()
    {
        CancellationTokenSource mine;
        try
        {
            mine = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        var previous = Interlocked.Exchange(ref resumeCts, mine);
        Guard(() => previous?.Cancel());
        try
        {
            await Task.Delay(resumeDelay, mine.Token);
            await controller.RefreshAsync(force: true);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Interlocked.CompareExchange(ref resumeCts, null, mine);
            mine.Dispose();
        }
    }

    public async Task BootstrapAsync(Action? prime = null)
    {
        try
        {
            if (prime is not null)
            {
                await Task.Run(prime);
            }
        }
        catch (Exception)
        {
        }

        try
        {
            await controller.BootstrapAsync();
        }
        catch (Exception)
        {
        }
    }

    public void Quit()
    {
        if (quit)
        {
            return;
        }

        quit = true;
        Guard(() => lifetime.Cancel());
        if (panel is not null)
        {
            panel.PropertyChanged -= OnPanelPropertyChanged;
        }

        Guard(controller.StopPolling);
        Guard(() => tray?.Dispose());
        foreach (var disposable in owned)
        {
            Guard(disposable.Dispose);
        }

        owned.Clear();
        Guard(() => panel?.CloseForShutdown());
        Guard(shutdown);
    }

    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
        }
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

    private bool TryCreateTray()
    {
        try
        {
            tray = createTray(Toggle, Activate, Quit);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private void SchedulePositionCorrection()
    {
        Dispatcher.UIThread.Post(() => Guard(() =>
        {
            if (!quit && panel is { IsVisible: true })
            {
                PositionPanel();
            }
        }), DispatcherPriority.Background);
    }

    private void OnPanelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Visual.BoundsProperty)
        {
            return;
        }

        Guard(() =>
        {
            if (quit || panel is not { IsVisible: true })
            {
                return;
            }

            var size = panel.Bounds.Size;
            if (lastPanelSize == size)
            {
                return;
            }

            lastPanelSize = size;
            PositionPanel();
        });
    }

    private ScreenInfo? CurrentScreen()
    {
        if (screenProvider is not null)
        {
            return screenProvider();
        }

        var screen = panel?.Screens.Primary ?? panel?.Screens.All.FirstOrDefault();
        return screen is null ? null : new ScreenInfo(screen.Bounds, screen.WorkingArea, screen.Scaling);
    }

    private void PositionPanel()
    {
        if (panel is null || !trayMode)
        {
            return;
        }

        var screen = CurrentScreen();
        if (screen is null)
        {
            return;
        }

        var maxHeight = Math.Max(1, (screen.WorkArea.Height - 2 * PanelPlacement.DefaultMargin) / Math.Max(screen.Scaling, 0.1));
        panel.SetMaxPanelHeight(maxHeight);
        var layout = PanelScreenPlacement.Compute(screen.Bounds, screen.WorkArea, screen.Scaling, DesiredHeightDips(maxHeight));
        panel.SetMaxPanelHeight(layout.MaxHeightDips);
        panel.Position = layout.Position;
    }

    private double DesiredHeightDips(double maxHeight)
    {
        if (panel is null)
        {
            return FallbackHeightDips;
        }

        if (panel.IsVisible && panel.Bounds.Height > 0)
        {
            return panel.Bounds.Height;
        }

        if (panel.Content is Control root)
        {
            root.Measure(new Size(PanelScreenPlacement.PanelWidthDips, maxHeight));
            if (root.DesiredSize.Height > 0)
            {
                return root.DesiredSize.Height;
            }
        }

        return FallbackHeightDips;
    }
}

internal static class AppIcon
{
    public static WindowIcon? Load()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://lg-ac/Assets/lg-ac.png"));
            return new WindowIcon(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
