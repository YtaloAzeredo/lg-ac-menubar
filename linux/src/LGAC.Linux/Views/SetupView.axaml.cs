using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LGAC.Core;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Views;

public partial class SetupView : UserControl, IDisposable, IPopupOwner
{
    private const string TokenPageUrl = "https://connect-pat.lgthinq.com";
    private const string DefaultCountry = "BR";

    private readonly PanelServices services;
    private readonly Func<Uri, Task> openUri;
    private readonly EffectiveVisibility visibility;

    public SetupView(PanelServices services, string? message, Func<Uri, Task>? openUri = null)
    {
        this.services = services;
        this.openUri = openUri ?? OpenWithSystemAsync;
        InitializeComponent();

        FillCountries(ReadCountry(services.Store));
        MessageText.Text = message ?? string.Empty;
        MessageText.IsVisible = message is not null;
        TokenBox.PropertyChanged += OnTokenPropertyChanged;
        CountryBox.DropDownClosed += OnDropDownClosed;
        visibility = new EffectiveVisibility(this, OnEffectivelyVisibleChanged);
        UpdateConnectState();
    }

    public event EventHandler? PopupClosed;

    public bool HasOpenPopup => CountryBox.IsDropDownOpen;

    public void Dispose()
    {
        visibility.Dispose();
        CountryBox.DropDownClosed -= OnDropDownClosed;
    }

    private void OnEffectivelyVisibleChanged(bool visible)
    {
        try
        {
            if (visible)
            {
                Dispatcher.UIThread.Post(FocusToken, DispatcherPriority.Input);
            }
            else
            {
                CountryBox.IsDropDownOpen = false;
            }
        }
        catch (Exception)
        {
        }
    }

    private static string ReadCountry(ICredentialStore store)
    {
        try
        {
            return store.Country;
        }
        catch (Exception)
        {
            return DefaultCountry;
        }
    }

    private void FillCountries(string selected)
    {
        ComboBoxItem? match = null;
        ComboBoxItem? fallback = null;
        foreach (var (code, name) in CountryNames.All)
        {
            var item = new ComboBoxItem { Content = name, Tag = code };
            CountryBox.Items.Add(item);
            if (string.Equals(code, selected, StringComparison.OrdinalIgnoreCase))
            {
                match = item;
            }

            if (code == DefaultCountry)
            {
                fallback = item;
            }
        }

        CountryBox.SelectedItem = match ?? fallback ?? (CountryBox.Items.Count > 0 ? CountryBox.Items[0] : null);
    }

    private string SelectedCountry()
    {
        return (CountryBox.SelectedItem as ComboBoxItem)?.Tag as string ?? DefaultCountry;
    }

    private void UpdateConnectState()
    {
        ConnectButton.IsEnabled = !string.IsNullOrEmpty(TokenBox.Text);
    }

    public void FocusToken()
    {
        try
        {
            if (visibility.IsVisible)
            {
                TokenBox.Focus();
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        PopupClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTokenPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty)
        {
            return;
        }

        try
        {
            UpdateConnectState();
        }
        catch (Exception)
        {
        }
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!ConnectButton.IsEnabled)
            {
                return;
            }

            var country = SelectedCountry();
            var token = TokenBox.Text ?? string.Empty;
            TokenBox.Text = string.Empty;
            await services.Controller.ConnectAsync(token, country);
        }
        catch (Exception)
        {
        }
    }

    private async void OnGenerateTokenClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await openUri(new Uri(TokenPageUrl));
        }
        catch (Exception)
        {
        }
    }

    private void OnQuitClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            services.Quit();
        }
        catch (Exception)
        {
        }
    }

    private Task OpenWithSystemAsync(Uri uri)
    {
        return UriOpener.OpenAsync(TopLevel.GetTopLevel(this)?.Launcher, uri);
    }
}
