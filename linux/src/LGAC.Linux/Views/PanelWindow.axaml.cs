using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using LGAC.Core;

namespace LGAC.Linux.Views;

public partial class PanelWindow : Window
{
    private const long ToggleGuardMilliseconds = 300;
    private const long ShowGuardMilliseconds = 200;
    private const double RecheckMilliseconds = 200;
    private const double PanelCornerRadius = 8;

    private readonly PanelServices services;
    private readonly bool trayMode;
    private readonly Func<long> tickClock;
    private readonly Func<bool> isActive;
    private readonly DispatcherTimer recheck = new() { Interval = TimeSpan.FromMilliseconds(RecheckMilliseconds) };
    private IPopupOwner? popupOwner;
    private long? shownAt;
    private ACPhase? shownPhase;
    private bool hiding;
    private bool closed;
    private bool shuttingDown;
    private long? lastFocusLossHide;

    public PanelWindow(PanelServices services, bool trayMode, Func<long>? tickClock = null, Func<bool>? isActive = null)
    {
        this.services = services;
        this.trayMode = trayMode;
        this.tickClock = tickClock ?? (() => Environment.TickCount64);
        this.isActive = isActive ?? (() => IsActive);
        InitializeComponent();
        ConfigureMode();

        recheck.Tick += (_, _) => Recheck();
        Deactivated += OnPanelDeactivated;
        Closing += OnPanelClosing;
        Closed += OnPanelClosed;
        KeyDown += OnPanelKeyDown;
        Opened += OnPanelOpened;
        services.Controller.PropertyChanged += OnControllerPropertyChanged;

        RenderPhase();
    }

    public Control? CurrentView => Host.Content as Control;

    public void SetMaxPanelHeight(double height)
    {
        MaxHeight = Math.Max(height, 1);
    }

    public void ShowPanel()
    {
        if (closed)
        {
            return;
        }

        RenderPhase();
        if (!trayMode && WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        if (!IsVisible)
        {
            shownAt = tickClock();
            Show();
        }

        Activate();
        Scroller.ScrollToHome();
        _ = RefreshOnOpenAsync();
    }

    public void HidePanel()
    {
        if (!trayMode || closed || !IsVisible)
        {
            return;
        }

        recheck.Stop();
        hiding = true;
        try
        {
            Hide();
        }
        finally
        {
            hiding = false;
        }
    }

    public void Toggle()
    {
        try
        {
            if (closed)
            {
                return;
            }

            if (!trayMode)
            {
                ShowPanel();
                return;
            }

            if (IsVisible)
            {
                HidePanel();
                return;
            }

            if (lastFocusLossHide is { } hiddenAt && tickClock() - hiddenAt < ToggleGuardMilliseconds)
            {
                return;
            }

            ShowPanel();
        }
        catch (Exception)
        {
        }
    }

    public void CloseForShutdown()
    {
        shuttingDown = true;
        if (!closed)
        {
            Close();
        }
    }

    internal void Recheck()
    {
        recheck.Stop();
        try
        {
            if (trayMode && !closed && IsVisible && !isActive() && popupOwner?.HasOpenPopup != true)
            {
                lastFocusLossHide = tickClock();
                HidePanel();
            }
        }
        catch (Exception)
        {
        }
    }

    internal void HandleDeactivated()
    {
        if (!trayMode || hiding || closed || !IsVisible)
        {
            return;
        }

        if (popupOwner?.HasOpenPopup == true)
        {
            return;
        }

        if (shownAt is { } shown && tickClock() - shown < ShowGuardMilliseconds)
        {
            ScheduleRecheck();
            return;
        }

        lastFocusLossHide = tickClock();
        HidePanel();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActualTransparencyLevelProperty)
        {
            UpdateCorners();
        }
    }

    private Control CreateReadyView()
    {
        return new ControlPanelView(services);
    }

    private void ConfigureMode()
    {
        if (trayMode)
        {
            WindowDecorations = WindowDecorations.None;
            Topmost = true;
            ShowInTaskbar = false;
            Background = Brushes.Transparent;
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
            return;
        }

        Root.CornerRadius = new CornerRadius(0);
        Root.BorderThickness = new Thickness(0);
    }

    private void UpdateCorners()
    {
        if (!trayMode)
        {
            return;
        }

        var transparent = ActualTransparencyLevel == WindowTransparencyLevel.Transparent;
        Root.CornerRadius = new CornerRadius(transparent ? PanelCornerRadius : 0);
    }

    private async Task RefreshOnOpenAsync()
    {
        try
        {
            await services.Controller.RefreshAsync();
        }
        catch (Exception)
        {
        }
    }

    private void RenderPhase()
    {
        var phase = services.Controller.Phase;
        if (Host.Content is not null && Equals(phase, shownPhase))
        {
            return;
        }

        var previous = Host.Content;
        Host.Content = CreateView(phase);
        shownPhase = phase;
        WatchPopups(Host.Content as IPopupOwner);
        (previous as IDisposable)?.Dispose();
        if (previous is not null)
        {
            Scroller.ScrollToHome();
        }
    }

    private void ScheduleRecheck()
    {
        recheck.Stop();
        recheck.Start();
    }

    private void WatchPopups(IPopupOwner? next)
    {
        recheck.Stop();
        if (popupOwner is not null)
        {
            popupOwner.PopupClosed -= OnPopupClosed;
        }

        popupOwner = next;
        if (popupOwner is not null)
        {
            popupOwner.PopupClosed += OnPopupClosed;
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (!trayMode || closed || !IsVisible || isActive())
        {
            return;
        }

        ScheduleRecheck();
    }

    private Control CreateView(ACPhase phase)
    {
        return phase switch
        {
            ACPhase.NeedsSetup setup => new SetupView(services, setup.Message),
            ACPhase.ChoosingDevice choosing => new DeviceChoiceView(services, choosing.Devices),
            ACPhase.Ready => CreateReadyView(),
            _ => new LoadingView(services),
        };
    }

    private void OnPanelOpened(object? sender, EventArgs e)
    {
        UpdateCorners();
    }

    private void OnPanelDeactivated(object? sender, EventArgs e)
    {
        try
        {
            HandleDeactivated();
        }
        catch (Exception)
        {
        }
    }

    private void OnPanelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!trayMode || e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        try
        {
            HidePanel();
        }
        catch (Exception)
        {
        }
    }

    private void OnPanelClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            shuttingDown = true;
        }

        if (!trayMode || shuttingDown)
        {
            return;
        }

        e.Cancel = true;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                HidePanel();
            }
            catch (Exception)
            {
            }
        });
    }

    private void OnPanelClosed(object? sender, EventArgs e)
    {
        closed = true;
        WatchPopups(null);
        services.Controller.PropertyChanged -= OnControllerPropertyChanged;
        try
        {
            (Host.Content as IDisposable)?.Dispose();
        }
        catch (Exception)
        {
        }

        if (trayMode || shuttingDown)
        {
            return;
        }

        try
        {
            services.Quit();
        }
        catch (Exception)
        {
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (null or nameof(ACController.Phase)))
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(SafeRenderPhase);
            return;
        }

        SafeRenderPhase();
    }

    private void SafeRenderPhase()
    {
        try
        {
            if (!closed)
            {
                RenderPhase();
            }
        }
        catch (Exception)
        {
        }
    }
}
