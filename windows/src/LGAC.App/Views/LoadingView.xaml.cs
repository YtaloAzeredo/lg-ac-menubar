using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LGAC.App.Platform;
using LGAC.Core;

namespace LGAC.App.Views;

public partial class LoadingView : UserControl
{
    private static readonly TimeSpan SpinnerPeriod = TimeSpan.FromSeconds(1);

    private readonly ACController controller;
    private readonly StartupRegistration startup;
    private readonly RotateTransform rotation = new();
    private ContextMenu? openMenu;
    private bool subscribed;

    public LoadingView(ACController controller, StartupRegistration startup)
    {
        this.controller = controller;
        this.startup = startup;
        InitializeComponent();
        Spinner.RenderTransform = rotation;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Render();
    }

    private void Render()
    {
        var message = controller.StatusMessage;
        MessageText.Text = message ?? string.Empty;
        MessagePanel.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        SwitchDeviceButton.Visibility = controller.CanSwitchDevice ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSpinner()
    {
        if (IsVisible)
        {
            var spin = new DoubleAnimation(0, 360, SpinnerPeriod) { RepeatBehavior = RepeatBehavior.Forever };
            rotation.BeginAnimation(RotateTransform.AngleProperty, spin);
        }
        else
        {
            rotation.BeginAnimation(RotateTransform.AngleProperty, null);
        }
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
        rotation.BeginAnimation(RotateTransform.AngleProperty, null);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (!IsVisible)
            {
                CloseMenu();
            }

            UpdateSpinner();
        }
        catch (Exception)
        {
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

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await controller.RefreshAsync(force: true);
        }
        catch (Exception)
        {
        }
    }

    private async void OnSwitchDeviceClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await controller.ChooseAnotherDeviceAsync();
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
