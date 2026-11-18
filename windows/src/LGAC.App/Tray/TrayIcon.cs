using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Threading;
using LGAC.App.Theme;
using LGAC.Core;
using Rect = System.Windows.Rect;

namespace LGAC.App.Tray;

public sealed class TrayIcon : IDisposable
{
    private const int MaxTipLength = 63;
    private const string DefaultTip = "Ar-condicionado";

    private readonly ACController controller;
    private readonly Action<Rect> onToggle;
    private readonly Dispatcher dispatcher;
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip menu;
    private Icon? currentIcon;
    private bool disposed;

    public TrayIcon(ACController controller, Action<Rect> onToggle)
    {
        this.controller = controller;
        this.onToggle = onToggle;
        dispatcher = Dispatcher.CurrentDispatcher;

        menu = new ContextMenuStrip();
        menu.Items.Add("Sair", null, OnExitClicked);

        notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Text = DefaultTip,
        };
        notifyIcon.MouseClick += OnMouseClick;

        UpdateAppearance();
        notifyIcon.Visible = true;

        controller.PropertyChanged += OnControllerPropertyChanged;
        SystemTheme.Changed += OnThemeChanged;
    }

    public PanelAnchor LastAnchor { get; private set; }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        controller.PropertyChanged -= OnControllerPropertyChanged;
        SystemTheme.Changed -= OnThemeChanged;
        notifyIcon.MouseClick -= OnMouseClick;
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        menu.Dispose();
        currentIcon?.Dispose();
        currentIcon = null;
    }

    private void OnExitClicked(object? sender, EventArgs e)
    {
        try
        {
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception)
        {
        }
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        try
        {
            var placement = PanelPlacement.AnchorFromCursor();
            LastAnchor = placement;
            onToggle(placement.Anchor);
        }
        catch (Exception)
        {
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ACController.State) or nameof(ACController.IsDeviceOffline) or nameof(ACController.DeviceAlias))
        {
            RunOnUiThread(UpdateAppearance);
        }
    }

    private void OnThemeChanged()
    {
        RunOnUiThread(UpdateAppearance);
    }

    private void RunOnUiThread(Action action)
    {
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }

    private void UpdateAppearance()
    {
        if (disposed)
        {
            return;
        }

        var state = controller.State;
        var active = state?.IsOn == true && !controller.IsDeviceOffline;
        var icon = ModeIcon.Render(state?.JobMode, !active, SystemTheme.IsTaskbarLight, SystemInformation.SmallIconSize.Width);
        var previous = currentIcon;
        notifyIcon.Icon = icon;
        currentIcon = icon;
        previous?.Dispose();

        notifyIcon.Text = TipFor(controller.DeviceAlias);
    }

    private static string TipFor(string alias)
    {
        var text = string.IsNullOrWhiteSpace(alias) ? DefaultTip : alias.Trim();
        if (text.Length <= MaxTipLength)
        {
            return text;
        }

        var length = char.IsHighSurrogate(text[MaxTipLength - 1]) ? MaxTipLength - 1 : MaxTipLength;
        return text[..length];
    }
}
