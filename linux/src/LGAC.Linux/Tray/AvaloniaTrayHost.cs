using Avalonia;
using Avalonia.Controls;

namespace LGAC.Linux.Tray;

internal sealed class AvaloniaTrayHost : ITrayHost
{
    private readonly TrayIcon trayIcon;
    private bool disposed;

    public AvaloniaTrayHost(Application application)
    {
        trayIcon = new TrayIcon();
        trayIcon.Clicked += OnClicked;
        TrayIcon.SetIcons(application, new TrayIcons { trayIcon });
        trayIcon.IsVisible = true;
    }

    public event Action? Clicked;

    public void SetIcon(WindowIcon icon)
    {
        trayIcon.Icon = icon;
    }

    public void SetToolTip(string text)
    {
        trayIcon.ToolTipText = text;
    }

    public void SetMenu(IReadOnlyList<TrayMenuEntry> entries)
    {
        var menu = new NativeMenu();
        foreach (var entry in entries)
        {
            var item = new NativeMenuItem(entry.Header);
            item.Click += (_, _) => entry.Invoke();
            menu.Items.Add(item);
        }

        trayIcon.Menu = menu;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        trayIcon.Clicked -= OnClicked;
        trayIcon.IsVisible = false;
        trayIcon.Dispose();
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        Clicked?.Invoke();
    }
}
