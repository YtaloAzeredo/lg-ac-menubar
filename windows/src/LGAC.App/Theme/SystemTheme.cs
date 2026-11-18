using System.IO;
using Microsoft.Win32;
using System.Windows.Threading;

namespace LGAC.App.Theme;

public static class SystemTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static Dispatcher? dispatcher;

    public static event Action? Changed;

    public static bool IsTaskbarLight => ReadLight("SystemUsesLightTheme", fallback: false);

    public static bool AppsUseLightTheme => ReadLight("AppsUseLightTheme", fallback: true);

    public static void Start()
    {
        if (dispatcher is not null)
        {
            return;
        }

        dispatcher = Dispatcher.CurrentDispatcher;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static void Stop()
    {
        if (dispatcher is null)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        dispatcher = null;
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
        {
            return;
        }

        dispatcher?.BeginInvoke(() => Changed?.Invoke());
    }

    private static bool ReadLight(string name, bool fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int value ? value != 0 : fallback;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return fallback;
        }
    }
}
