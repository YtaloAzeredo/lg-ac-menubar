using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace LGAC.Linux.Views;

public partial class LoadingView : UserControl, IDisposable, IPopupOwner
{
    private readonly PanelServices services;
    private readonly EffectiveVisibility visibility;
    private readonly MenuSlot menus = new();
    private bool disposed;

    public LoadingView(PanelServices services)
    {
        this.services = services;
        InitializeComponent();

        visibility = new EffectiveVisibility(this, OnEffectivelyVisibleChanged);
        menus.Closed += OnMenuClosed;
        services.Controller.PropertyChanged += OnControllerPropertyChanged;
        Render();
        UpdateSpinner();
    }

    public event EventHandler? PopupClosed;

    public bool HasOpenPopup => menus.IsOpen;

    internal ContextMenu? OpenMenu => menus.Menu;

    internal bool IsSpinning => Spinner.Classes.Contains("spinning");

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
        UpdateSpinner();
    }

    private void OnEffectivelyVisibleChanged(bool visible)
    {
        try
        {
            if (!visible)
            {
                CloseMenu();
            }

            UpdateSpinner();
        }
        catch (Exception)
        {
        }
    }

    private void Render()
    {
        var message = services.Controller.StatusMessage;
        MessageText.Text = message ?? string.Empty;
        MessagePanel.IsVisible = message is not null;
        SwitchDeviceButton.IsVisible = services.Controller.CanSwitchDevice;
    }

    private void UpdateSpinner()
    {
        Spinner.Classes.Set("spinning", !disposed && visibility.IsVisible);
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

    private async void OnRetryClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await services.Controller.RefreshAsync(force: true);
        }
        catch (Exception)
        {
        }
    }

    private async void OnSwitchDeviceClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await services.Controller.ChooseAnotherDeviceAsync();
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
