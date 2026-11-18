using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LGAC.App.Platform;
using LGAC.Core;

namespace LGAC.App.Views;

public static class SettingsMenu
{
    public static ContextMenu Open(FrameworkElement target, ACController controller, StartupRegistration startup, PlacementMode placement)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = placement };

        if (controller.CanSwitchDevice)
        {
            var switchItem = new MenuItem { Header = "Trocar aparelho…" };
            switchItem.Click += async (_, _) =>
            {
                try
                {
                    await controller.ChooseAnotherDeviceAsync();
                }
                catch (Exception)
                {
                }
            };
            menu.Items.Add(switchItem);
        }

        var tokenItem = new MenuItem { Header = "Trocar token…" };
        tokenItem.Click += (_, _) =>
        {
            try
            {
                controller.ResetCredentials();
            }
            catch (Exception)
            {
            }
        };
        menu.Items.Add(tokenItem);

        var startupItem = new MenuItem
        {
            Header = "Iniciar com o Windows",
            IsCheckable = true,
            IsChecked = IsStartupEnabled(startup),
        };
        startupItem.Click += (_, _) => ToggleStartup(startupItem, startup);
        menu.Items.Add(startupItem);

        menu.Items.Add(new Separator());

        var quitItem = new MenuItem { Header = "Sair" };
        quitItem.Click += (_, _) => Quit();
        menu.Items.Add(quitItem);

        menu.IsOpen = true;
        return menu;
    }

    public static void Quit()
    {
        try
        {
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception)
        {
        }
    }

    private static void ToggleStartup(MenuItem item, StartupRegistration startup)
    {
        try
        {
            startup.IsEnabled = !IsStartupEnabled(startup);
        }
        catch (Exception)
        {
        }

        try
        {
            item.IsChecked = IsStartupEnabled(startup);
        }
        catch (Exception)
        {
        }
    }

    private static bool IsStartupEnabled(StartupRegistration startup)
    {
        try
        {
            return startup.IsEnabled;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
