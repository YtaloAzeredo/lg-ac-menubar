using Avalonia.Controls;
using Avalonia.Threading;

namespace LGAC.Linux.Views;

public static class SettingsMenu
{
    public static ContextMenu Build(PanelServices services)
    {
        var menu = new ContextMenu();

        if (services.Controller.CanSwitchDevice)
        {
            var switchItem = new MenuItem { Header = "Trocar aparelho…" };
            switchItem.Click += async (_, _) =>
            {
                try
                {
                    await services.Controller.ChooseAnotherDeviceAsync();
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
                services.Controller.ResetCredentials();
            }
            catch (Exception)
            {
            }
        };
        menu.Items.Add(tokenItem);

        var autostartItem = new MenuItem
        {
            Header = "Iniciar com o sistema",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = IsAutostartEnabled(services),
        };
        autostartItem.Click += (_, _) => ToggleAutostart(autostartItem, services);
        menu.Items.Add(autostartItem);

        menu.Items.Add(new Separator());

        var quitItem = new MenuItem { Header = "Sair" };
        quitItem.Click += (_, _) => Quit(services);
        menu.Items.Add(quitItem);

        return menu;
    }

    private static void Quit(PanelServices services)
    {
        try
        {
            services.Quit();
        }
        catch (Exception)
        {
        }
    }

    private static void ToggleAutostart(MenuItem item, PanelServices services)
    {
        try
        {
            services.Autostart.IsEnabled = !IsAutostartEnabled(services);
        }
        catch (Exception)
        {
        }

        SyncChecked(item, services);
        Dispatcher.UIThread.Post(() => SyncChecked(item, services));
    }

    private static void SyncChecked(MenuItem item, PanelServices services)
    {
        try
        {
            item.IsChecked = IsAutostartEnabled(services);
        }
        catch (Exception)
        {
        }
    }

    private static bool IsAutostartEnabled(PanelServices services)
    {
        try
        {
            return services.Autostart.IsEnabled;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
