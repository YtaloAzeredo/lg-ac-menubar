using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using LGAC.Core;

namespace LGAC.Linux.Views;

public partial class DeviceChoiceView : UserControl, IDisposable, IPopupOwner
{
    private readonly PanelServices services;
    private readonly EffectiveVisibility visibility;
    private readonly MenuSlot menus = new();
    private bool disposed;

    public DeviceChoiceView(PanelServices services, IReadOnlyList<ThinQDevice> devices)
    {
        this.services = services;
        InitializeComponent();

        foreach (var device in devices)
        {
            DeviceList.Children.Add(CreateDeviceButton(device));
        }

        visibility = new EffectiveVisibility(this, OnEffectivelyVisibleChanged);
        menus.Closed += OnMenuClosed;
        services.Controller.PropertyChanged += OnControllerPropertyChanged;
        Render();
    }

    public event EventHandler? PopupClosed;

    public bool HasOpenPopup => menus.IsOpen;

    internal ContextMenu? OpenMenu => menus.Menu;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        visibility.Dispose();
        services.Controller.PropertyChanged -= OnControllerPropertyChanged;
        CloseMenu();
    }

    private void OnEffectivelyVisibleChanged(bool visible)
    {
        if (!visible)
        {
            CloseMenu();
        }
    }

    private void Render()
    {
        CancelButton.IsVisible = services.Controller.IsSwitchingDevice;
    }

    private Button CreateDeviceButton(ThinQDevice device)
    {
        var name = string.IsNullOrEmpty(device.Alias) ? device.ModelName : device.Alias;
        var title = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis };
        var model = new TextBlock { Text = device.ModelName, TextTrimming = TextTrimming.CharacterEllipsis };
        model.Classes.Add("secondary");

        var content = new StackPanel();
        content.Children.Add(title);
        content.Children.Add(model);

        var button = new Button { Content = content, Tag = device, Margin = new Thickness(0, 0, 0, 8) };
        button.Classes.Add("list");
        AutomationProperties.SetName(button, name);
        button.Click += OnDeviceClick;
        return button;
    }

    private void CloseMenu()
    {
        menus.Close();
    }

    private void OnMenuClosed(object? sender, EventArgs e)
    {
        PopupClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(SafeRender);
            return;
        }

        SafeRender();
    }

    private void SafeRender()
    {
        try
        {
            if (!disposed)
            {
                Render();
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnDeviceClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Control { Tag: ThinQDevice device })
            {
                await services.Controller.SelectDeviceAsync(device);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await services.Controller.CancelDeviceChoiceAsync();
        }
        catch (Exception)
        {
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            CloseMenu();
            var menu = SettingsMenu.Build(services);
            menu.Placement = PlacementMode.Bottom;
            menus.Open(menu, SettingsButton);
        }
        catch (Exception)
        {
        }
    }
}
