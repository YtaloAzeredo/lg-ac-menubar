using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using LGAC.App.Platform;
using LGAC.App.Theme;
using LGAC.App.Tray;
using LGAC.Core;

namespace LGAC.App.Views;

public partial class PanelWindow : Window
{
    private const double PanelWidth = 320;
    private const long ToggleGuardMilliseconds = 300;
    private const double SizeTolerance = 0.01;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080;
    private const long WsExAppWindow = 0x00040000;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZOrder = 0x0200;

    private static readonly IntPtr HwndTopmost = new(-1);

    private static readonly (string Key, uint Light, uint Dark)[] Palette =
    [
        ("PanelBackgroundBrush", 0xFFF9F9F9, 0xFF2B2B2B),
        ("PanelBorderBrush", 0xFFD0D0D0, 0xFF474747),
        ("PanelForegroundBrush", 0xFF1B1B1B, 0xFFF3F3F3),
        ("SecondaryForegroundBrush", 0xFF5C5C5C, 0xFFB8B8B8),
        ("AccentBrush", 0xFF005FB8, 0xFF60CDFF),
        ("AccentForegroundBrush", 0xFFFFFFFF, 0xFF000000),
        ("WarningBrush", 0xFFB04F00, 0xFFFFA338),
        ("ErrorBrush", 0xFFC42B1C, 0xFFFF99A4),
        ("ControlBackgroundBrush", 0xFFE6E6E6, 0xFF3D3D3D),
        ("InputBackgroundBrush", 0xFFFFFFFF, 0xFF1F1F1F),
        ("InputBorderBrush", 0xFFBDBDBD, 0xFF5C5C5C),
        ("MenuBackgroundBrush", 0xFFF9F9F9, 0xFF2B2B2B),
        ("MenuHoverBrush", 0xFFDCDCDC, 0xFF474747),
        ("DividerBrush", 0xFFE0E0E0, 0xFF3D3D3D),
        ("ScrollThumbBrush", 0xFF9E9E9E, 0xFF7A7A7A),
        ("SwitchOffBrush", 0xFF8C8C8C, 0xFF808080),
        ("SwitchKnobBrush", 0xFFFFFFFF, 0xFFE6E6E6),
        ("FocusBrush", 0xFF1B1B1B, 0xFFF3F3F3),
    ];

    private readonly ACController controller;
    private readonly ICredentialStore store;
    private readonly StartupRegistration startup;
    private ACPhase? shownPhase;
    private PanelAnchor anchor;
    private bool hasAnchor;
    private bool hiding;
    private bool closed;
    private bool repositionPending;
    private long? lastFocusLossHide;
    private bool? appliedLightTheme;

    public PanelWindow(ACController controller, ICredentialStore store, StartupRegistration startup)
    {
        this.controller = controller;
        this.store = store;
        this.startup = startup;
        ApplyTheme();
        InitializeComponent();

        Host.SizeChanged += OnHostSizeChanged;
        Deactivated += OnPanelDeactivated;
        Closing += OnPanelClosing;
        Closed += OnPanelClosed;
        KeyDown += OnPanelKeyDown;
        DpiChanged += OnPanelDpiChanged;
        controller.PropertyChanged += OnControllerPropertyChanged;
        SystemTheme.Changed += OnSystemThemeChanged;

        RenderPhase();
    }

    public void Toggle(PanelAnchor target)
    {
        try
        {
            if (closed)
            {
                return;
            }

            if (IsVisible)
            {
                HidePanel();
                return;
            }

            if (lastFocusLossHide is { } hiddenAt && Environment.TickCount64 - hiddenAt < ToggleGuardMilliseconds)
            {
                return;
            }

            ShowPanel(target);
        }
        catch (Exception)
        {
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            HideFromAltTab(new WindowInteropHelper(this).Handle);
        }
        catch (Exception)
        {
        }
    }

    private void ShowPanel(PanelAnchor target)
    {
        anchor = target;
        hasAnchor = true;
        ApplyTheme();
        RenderPhase();
        Scroller.ScrollToHome();

        var handle = new WindowInteropHelper(this).EnsureHandle();
        var bounds = BoundsFor(MeasureContentHeight());
        MoveTo(handle, bounds);
        Show();
        MoveTo(handle, bounds);
        Activate();

        _ = RefreshOnOpenAsync();
    }

    private void HidePanel()
    {
        if (closed || !IsVisible)
        {
            return;
        }

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

    private async Task RefreshOnOpenAsync()
    {
        try
        {
            await controller.RefreshAsync();
        }
        catch (Exception)
        {
        }
    }

    private void RenderPhase()
    {
        var phase = controller.Phase;
        if (Host.Content is not null && Equals(phase, shownPhase))
        {
            return;
        }

        var previous = Host.Content;
        Host.Content = CreateView(phase);
        shownPhase = phase;
        (previous as IDisposable)?.Dispose();
        if (previous is not null)
        {
            Scroller.ScrollToHome();
        }
    }

    private FrameworkElement CreateView(ACPhase phase)
    {
        return phase switch
        {
            ACPhase.NeedsSetup setup => new SetupView(controller, store, setup.Message),
            ACPhase.ChoosingDevice choosing => new DeviceChoiceView(controller, startup, choosing.Devices),
            ACPhase.Ready => new ControlPanelView(controller, startup),
            _ => new LoadingView(controller, startup),
        };
    }

    private double MeasureContentHeight()
    {
        Root.Measure(new Size(PanelWidth, double.PositiveInfinity));
        var height = Root.DesiredSize.Height;
        Root.InvalidateMeasure();
        return height;
    }

    private Rect BoundsFor(double contentHeight)
    {
        return PanelPlacement.Compute(anchor.WorkArea, anchor.Anchor, new Size(PanelWidth, Math.Max(0, contentHeight)));
    }

    private void MoveTo(IntPtr handle, Rect bounds)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var scale = anchor.Scale > 0 ? anchor.Scale : 1.0;
        var left = (int)Math.Round(bounds.Left * scale);
        var top = (int)Math.Round(bounds.Top * scale);
        var width = (int)Math.Ceiling(bounds.Width * scale - SizeTolerance);
        var height = (int)Math.Ceiling(bounds.Height * scale - SizeTolerance);
        SetWindowPos(handle, HwndTopmost, left, top, width, height, SwpNoActivate | SwpNoOwnerZOrder);
    }

    private void ScheduleReposition()
    {
        if (repositionPending || closed)
        {
            return;
        }

        repositionPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            repositionPending = false;
            try
            {
                Reposition();
            }
            catch (Exception)
            {
            }
        }));
    }

    private void Reposition()
    {
        if (closed || !IsVisible || !hasAnchor)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        var chrome = Root.BorderThickness.Top + Root.BorderThickness.Bottom + Root.Padding.Top + Root.Padding.Bottom;
        MoveTo(handle, BoundsFor(Host.ActualHeight + chrome));
    }

    private void ApplyTheme()
    {
        var light = SystemTheme.AppsUseLightTheme;
        if (appliedLightTheme == light)
        {
            return;
        }

        appliedLightTheme = light;
        var resources = System.Windows.Application.Current.Resources;
        foreach (var (key, lightColor, darkColor) in Palette)
        {
            var brush = new SolidColorBrush(ToColor(light ? lightColor : darkColor));
            brush.Freeze();
            resources[key] = brush;
        }
    }

    private static Color ToColor(uint argb)
    {
        return Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsVisible)
        {
            ScheduleReposition();
        }
    }

    private void OnPanelDpiChanged(object sender, DpiChangedEventArgs e)
    {
        if (IsVisible)
        {
            ScheduleReposition();
        }
    }

    private void OnPanelDeactivated(object? sender, EventArgs e)
    {
        try
        {
            if (hiding || closed || !IsVisible)
            {
                return;
            }

            lastFocusLossHide = Environment.TickCount64;
            HidePanel();
        }
        catch (Exception)
        {
        }
    }

    private void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
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

    private void OnPanelClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                HidePanel();
            }
            catch (Exception)
            {
            }
        }));
    }

    private void OnPanelClosed(object? sender, EventArgs e)
    {
        closed = true;
        controller.PropertyChanged -= OnControllerPropertyChanged;
        SystemTheme.Changed -= OnSystemThemeChanged;
        try
        {
            (Host.Content as IDisposable)?.Dispose();
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

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(SafeRenderPhase));
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

    private void OnSystemThemeChanged()
    {
        try
        {
            ApplyTheme();
        }
        catch (Exception)
        {
        }
    }

    private static void HideFromAltTab(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var style = IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, GwlExStyle).ToInt64()
            : GetWindowLong32(handle, GwlExStyle);
        var updated = (style | WsExToolWindow) & ~WsExAppWindow;
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr64(handle, GwlExStyle, new IntPtr(updated));
        }
        else
        {
            SetWindowLong32(handle, GwlExStyle, unchecked((int)updated));
        }
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
