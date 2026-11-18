using Avalonia.Controls;

namespace LGAC.Linux.Tray;

internal sealed record TrayMenuEntry(string Header, Action Invoke);

internal interface ITrayHost : IDisposable
{
    event Action? Clicked;

    void SetIcon(WindowIcon icon);

    void SetToolTip(string text);

    void SetMenu(IReadOnlyList<TrayMenuEntry> entries);
}
