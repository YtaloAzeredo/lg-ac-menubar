using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LGAC.App.Platform;
using LGAC.Core;

namespace LGAC.App.Views;

public partial class SetupView : UserControl
{
    private const string TokenPageUrl = "https://connect-pat.lgthinq.com";
    private const string DefaultCountry = "BR";

    private readonly ACController controller;

    public SetupView(ACController controller, ICredentialStore store, string? message)
    {
        this.controller = controller;
        InitializeComponent();

        HeaderGlyph.Child = ControlPanelView.CreateModeGlyph("COOL");
        FillCountries(ReadCountry(store));
        MessageText.Text = message ?? string.Empty;
        MessageText.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateConnectState();

        IsVisibleChanged += OnIsVisibleChanged;
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
        using var secure = TokenBox.SecurePassword;
        var hasToken = secure.Length > 0;
        ConnectButton.IsEnabled = hasToken;
        TokenPlaceholder.Visibility = hasToken ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FocusToken()
    {
        try
        {
            if (IsVisible)
            {
                TokenBox.Focus();
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (IsVisible)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusToken));
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

    private void OnTokenChanged(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateConnectState();
        }
        catch (Exception)
        {
        }
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!ConnectButton.IsEnabled)
            {
                return;
            }

            var country = SelectedCountry();
            var token = TokenBox.Password;
            TokenBox.Clear();
            await controller.ConnectAsync(token, country);
        }
        catch (Exception)
        {
        }
    }

    private void OnGenerateTokenClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(TokenPageUrl) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private void OnQuitClick(object sender, RoutedEventArgs e)
    {
        SettingsMenu.Quit();
    }
}
