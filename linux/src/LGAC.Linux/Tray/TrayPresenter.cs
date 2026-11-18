using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using LGAC.Core;

namespace LGAC.Linux.Tray;

internal sealed class TrayPresenter : IDisposable
{
    internal const int IconSize = 44;
    private const string DefaultTip = "Ar-condicionado";

    private readonly ACController controller;
    private readonly Action toggle;
    private readonly ITrayHost host;
    private readonly Func<string?, bool, int, WindowIcon> render;
    private bool disposed;

    public TrayPresenter(ACController controller, Action toggle, Action show, Action quit)
        : this(controller, toggle, show, quit, new AvaloniaTrayHost(Avalonia.Application.Current!))
    {
    }

    internal TrayPresenter(
        ACController controller,
        Action toggle,
        Action show,
        Action quit,
        ITrayHost host,
        Func<string?, bool, int, WindowIcon>? render = null)
    {
        this.controller = controller;
        this.toggle = toggle;
        this.host = host;
        this.render = render ?? ModeIcon.Render;

        try
        {
            host.SetMenu(
            [
                new TrayMenuEntry("Abrir painel", () => Guard(show)),
                new TrayMenuEntry("Sair", () => Guard(quit)),
            ]);
            host.Clicked += OnClicked;
            UpdateAppearance();
            controller.PropertyChanged += OnControllerPropertyChanged;
        }
        catch (Exception)
        {
            host.Clicked -= OnClicked;
            controller.PropertyChanged -= OnControllerPropertyChanged;
            Guard(host.Dispose);
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        controller.PropertyChanged -= OnControllerPropertyChanged;
        host.Clicked -= OnClicked;
        try
        {
            host.Dispose();
        }
        catch (Exception)
        {
        }
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

    private static string TipFor(string? alias)
    {
        return string.IsNullOrWhiteSpace(alias) ? DefaultTip : alias.Trim();
    }

    private void OnClicked()
    {
        Guard(toggle);
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (null or "" or nameof(ACController.State) or nameof(ACController.IsDeviceOffline) or nameof(ACController.DeviceAlias)))
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateAppearance();
            return;
        }

        Dispatcher.UIThread.Post(UpdateAppearance);
    }

    private void UpdateAppearance()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            var state = controller.State;
            var active = state?.IsOn == true && !controller.IsDeviceOffline;
            host.SetIcon(render(state?.JobMode, !active, IconSize));
            host.SetToolTip(TipFor(controller.DeviceAlias));
        }
        catch (Exception)
        {
        }
    }
}
