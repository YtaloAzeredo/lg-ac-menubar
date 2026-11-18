using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace LGAC.Linux.Tests.Support;

internal static class UiTesting
{
    private static readonly List<Window> Opened = [];

    public static void CloseAll()
    {
        var windows = Opened.ToList();
        Opened.Clear();
        foreach (var window in windows)
        {
            try
            {
                window.Close();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    public static Window Host(Control content, double width = 320)
    {
        var window = new Window
        {
            Width = width,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Content = content,
        };
        Opened.Add(window);
        window.Show();
        return window;
    }

    public static void Click(Window window, Control target)
    {
        window.UpdateLayout();
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("O controle não está na janela");
        window.MouseMove(center);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    public static IReadOnlyList<string> VisibleTexts(Control root)
    {
        return root.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && !string.IsNullOrEmpty(block.Text))
            .Select(block => block.Text!)
            .ToList();
    }

    public static IReadOnlyList<Button> VisibleButtons(Control root)
    {
        return root.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .ToList();
    }

    public static void CaptureBoth(TopLevel window, string name)
    {
        var application = Application.Current ?? throw new InvalidOperationException("Sem aplicação");
        var original = application.RequestedThemeVariant;
        try
        {
            application.RequestedThemeVariant = ThemeVariant.Light;
            window.UpdateLayout();
            HeadlessApp.Capture(window, name + "-light");
            application.RequestedThemeVariant = ThemeVariant.Dark;
            window.UpdateLayout();
            HeadlessApp.Capture(window, name + "-dark");
        }
        finally
        {
            application.RequestedThemeVariant = original;
        }
    }
}
