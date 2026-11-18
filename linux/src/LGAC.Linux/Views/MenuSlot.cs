using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LGAC.Linux.Views;

internal sealed class MenuSlot
{
    public event EventHandler? Closed;

    public ContextMenu? Menu { get; private set; }

    public bool IsOpen => Menu is { IsOpen: true };

    public void Open(ContextMenu next, Control target)
    {
        Close();
        Menu = next;
        next.Closed += OnMenuClosed;
        next.Open(target);
    }

    public void Close()
    {
        if (Menu is not { } current)
        {
            return;
        }

        current.Closed -= OnMenuClosed;
        if (current.IsOpen)
        {
            current.Close();
        }

        Menu = null;
    }

    private void OnMenuClosed(object? sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            menu.Closed -= OnMenuClosed;
        }

        Closed?.Invoke(this, EventArgs.Empty);
    }
}
