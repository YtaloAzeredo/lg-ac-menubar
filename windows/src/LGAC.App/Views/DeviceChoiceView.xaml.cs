using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LGAC.App.Platform;
using LGAC.Core;

namespace LGAC.App.Views;

public partial class DeviceChoiceView : UserControl
{
    private const string ListButtonStyleKey = "ListButton";
    private const string SecondaryForegroundBrushKey = "SecondaryForegroundBrush";

    private readonly ACController controller;
    private readonly StartupRegistration startup;
    private ContextMenu? openMenu;
    private bool subscribed;

    public DeviceChoiceView(ACController controller, StartupRegistration startup, IReadOnlyList<ThinQDevice> devices)
    {
        this.controller = controller;
        this.startup = startup;
        InitializeComponent();

        foreach (var device in devices)
        {
            DeviceList.Children.Add(CreateDeviceButton(device));
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Render();
    }

    private void Render()
    {
        CancelButton.Visibility = controller.IsSwitchingDevice ? Visibility.Visible : Visibility.Collapsed;
    }

    private Button CreateDeviceButton(ThinQDevice device)
    {
        var name = string.IsNullOrEmpty(device.Alias) ? device.ModelName : device.Alias;
        var title = new TextBlock { Text = name, TextTrimming = TextTrimming.CharacterEllipsis };
        var model = new TextBlock { Text = device.ModelName, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
        model.SetResourceReference(TextBlock.ForegroundProperty, SecondaryForegroundBrushKey);

        var content = new StackPanel();
        content.Children.Add(title);
        content.Children.Add(model);

        var button = new Button { Content = content, Tag = device, Margin = new Thickness(0, 0, 0, 8) };
        button.SetResourceReference(FrameworkElement.StyleProperty, ListButtonStyleKey);
        AutomationProperties.SetName(button, name);
        button.Click += OnDeviceClick;
        return button;
    }

    private void CloseMenu()
    {
        if (openMenu is { IsOpen: true } menu)
        {
            menu.IsOpen = false;
        }

        openMenu = null;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed)
        {
            controller.PropertyChanged += OnControllerPropertyChanged;
            subscribed = true;
        }

        SafeRender();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (subscribed)
        {
            controller.PropertyChanged -= OnControllerPropertyChanged;
            subscribed = false;
        }

        CloseMenu();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            CloseMenu();
        }
    }

    private void OnControllerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(SafeRender));
            return;
        }

        SafeRender();
    }

    private void SafeRender()
    {
        try
        {
            Render();
        }
        catch (Exception)
        {
        }
    }

    private async void OnDeviceClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { Tag: ThinQDevice device })
            {
                await controller.SelectDeviceAsync(device);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnCancelClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await controller.CancelDeviceChoiceAsync();
        }
        catch (Exception)
        {
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CloseMenu();
            openMenu = SettingsMenu.Open(SettingsButton, controller, startup, PlacementMode.Bottom);
        }
        catch (Exception)
        {
        }
    }
}
