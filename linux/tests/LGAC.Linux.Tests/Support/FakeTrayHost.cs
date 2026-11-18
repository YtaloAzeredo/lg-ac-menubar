using Avalonia.Controls;
using LGAC.Linux.Tray;

namespace LGAC.Linux.Tests.Support;

internal sealed class FakeTrayHost : ITrayHost
{
    public List<WindowIcon> Icons { get; } = [];

    public List<string> ToolTips { get; } = [];

    public IReadOnlyList<TrayMenuEntry> Menu { get; private set; } = [];

    public int DisposeCount { get; private set; }

    public Exception? SetIconError { get; set; }

    public Exception? SetMenuError { get; set; }

    public event Action? Clicked;

    public void SetIcon(WindowIcon icon)
    {
        if (SetIconError is not null)
        {
            throw SetIconError;
        }

        Icons.Add(icon);
    }

    public void SetToolTip(string text)
    {
        ToolTips.Add(text);
    }

    public void SetMenu(IReadOnlyList<TrayMenuEntry> entries)
    {
        if (SetMenuError is not null)
        {
            throw SetMenuError;
        }

        Menu = entries;
    }

    public void Click()
    {
        Clicked?.Invoke();
    }

    public void Choose(string header)
    {
        Menu.Single(entry => entry.Header == header).Invoke();
    }

    public void Dispose()
    {
        DisposeCount++;
    }
}
