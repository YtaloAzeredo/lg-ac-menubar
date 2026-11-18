using System.Diagnostics;
using Avalonia.Platform.Storage;

namespace LGAC.Linux.Views;

internal static class UriOpener
{
    public static async Task OpenAsync(ILauncher? launcher, Uri uri, Func<Uri, Task>? fallback = null)
    {
        if (launcher is not null)
        {
            try
            {
                if (await launcher.LaunchUriAsync(uri))
                {
                    return;
                }
            }
            catch (Exception)
            {
            }
        }

        try
        {
            await (fallback ?? OpenWithXdgAsync)(uri);
        }
        catch (Exception)
        {
        }
    }

    private static Task OpenWithXdgAsync(Uri uri)
    {
        using var process = Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { uri.AbsoluteUri }, UseShellExecute = false });
        return Task.CompletedTask;
    }
}
