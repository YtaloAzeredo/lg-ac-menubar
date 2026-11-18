using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LGAC.Core;
using LGAC.Linux.Platform;

namespace LGAC.Linux.Views;

public partial class ControlPanelView : UserControl, IDisposable, IPopupOwner
{
    private const double DimmedOpacity = 0.45;
    private const int MaxModeColumns = 5;
    private const int MaxWindColumns = 4;

    private static readonly int[] TimerOptions = [30, 60, 120, 180, 240, 360, 480];

    private readonly PanelServices services;
    private readonly Func<DateTimeOffset> now;
    private readonly DispatcherTimer clock;
    private readonly EffectiveVisibility visibility;
    private readonly MenuSlot menus = new();
    private bool rendering;
    private bool disposed;

    public ControlPanelView(PanelServices services, Func<DateTimeOffset>? now = null)
    {
        this.services = services;
        this.now = now ?? (() => DateTimeOffset.UtcNow);
        InitializeComponent();

        clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clock.Tick += OnClockTick;
        visibility = new EffectiveVisibility(this, OnEffectivelyVisibleChanged);
        menus.Closed += OnMenuClosed;
        services.Controller.PropertyChanged += OnControllerPropertyChanged;
        Render();
        UpdateClock();
    }

    public event EventHandler? PopupClosed;

    public bool HasOpenPopup => menus.IsOpen;

    internal ContextMenu? OpenMenu => menus.Menu;

    internal bool IsClockRunning => clock.IsEnabled;

    private ACController Controller => services.Controller;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        visibility.Dispose();
        Controller.PropertyChanged -= OnControllerPropertyChanged;
        clock.Tick -= OnClockTick;
        clock.Stop();
        CloseMenu();
    }

    internal void TickClock()
    {
        try
        {
            if (!disposed)
            {
                RenderFooter();
            }
        }
        catch (Exception)
        {
        }
    }

    private void Render()
    {
        rendering = true;
        try
        {
            RenderCore();
        }
        finally
        {
            rendering = false;
        }
    }

    private void RenderCore()
    {
        var caps = Controller.Capabilities;
        var state = Controller.State ?? new ACState();
        var isOn = state.IsOn == true;

        SwitchDeviceButton.IsVisible = Controller.CanSwitchDevice;
        AliasText.Text = string.IsNullOrEmpty(Controller.DeviceAlias) ? "Ar-condicionado" : Controller.DeviceAlias;
        if (state.CurrentTemp is { } current)
        {
            AmbientText.Text = "Ambiente " + ACLabels.Temperature(current);
            AmbientText.IsVisible = true;
        }
        else
        {
            AmbientText.Text = string.Empty;
            AmbientText.IsVisible = false;
        }

        var powerLabel = isOn ? "Desligar" : "Ligar";
        PowerButton.IsEnabled = caps?.Power == true;
        PowerButton.Classes.Set("on", isOn);
        ToolTip.SetTip(PowerButton, powerLabel);
        AutomationProperties.SetName(PowerButton, powerLabel);

        Body.IsEnabled = isOn;
        Body.Opacity = isOn ? 1 : DimmedOpacity;

        TemperatureRow.IsVisible = caps?.TargetTemp is not null;
        TargetTempText.Text = state.TargetTemp is { } target ? ACLabels.Temperature(target) : "–";

        RenderChoices(ModeSection, ModeGrid, caps?.JobModes ?? [], state.JobMode, MaxModeColumns, true, ACLabels.JobMode, OnModeChanged);
        RenderChoices(WindSection, WindGrid, caps?.WindStrengths ?? [], state.WindStrength, MaxWindColumns, false, ACLabels.WindStrength, OnWindChanged);

        SwingUpDownRow.IsVisible = caps?.SwingUpDown == true;
        SwingUpDownToggle.IsChecked = state.SwingUpDown ?? false;
        SwingLeftRightRow.IsVisible = caps?.SwingLeftRight == true;
        SwingLeftRightToggle.IsChecked = state.SwingLeftRight ?? false;
        DisplayLightRow.IsVisible = caps?.DisplayLight is not null;
        DisplayLightToggle.IsChecked = state.DisplayLightOn ?? false;
        TimerRow.IsVisible = caps?.OffTimer is not null;
        var timerLabel = state.OffTimerRemainingMinutes is { } minutes ? ACLabels.Duration(minutes) : "Desativado";
        TimerText.Text = timerLabel;
        AutomationProperties.SetName(TimerButton, "Desligar em: " + timerLabel);
        Switches.IsVisible = SwingUpDownRow.IsVisible
            || SwingLeftRightRow.IsVisible
            || DisplayLightRow.IsVisible
            || TimerRow.IsVisible;

        RenderFooter();
    }

    private void RenderFooter()
    {
        if (Controller.StatusMessage is { } message)
        {
            StatusText.Text = message;
            StatusPanel.IsVisible = true;
            UpdatedText.IsVisible = false;
        }
        else if (Controller.LastUpdated is { } updated)
        {
            StatusPanel.IsVisible = false;
            UpdatedText.Text = "Atualizado há " + ElapsedText.Format(now() - updated);
            UpdatedText.IsVisible = true;
        }
        else
        {
            StatusPanel.IsVisible = false;
            UpdatedText.IsVisible = false;
        }
    }

    private static void RenderChoices(
        Control section,
        UniformGrid grid,
        IReadOnlyList<string> options,
        string? selected,
        int maxColumns,
        bool withIcon,
        Func<string, string> title,
        EventHandler<RoutedEventArgs> onClick)
    {
        section.IsVisible = options.Count > 0;
        var current = grid.Children.OfType<RadioButton>().Select(button => button.Tag as string).ToList();
        if (!current.SequenceEqual(options))
        {
            grid.Children.Clear();
            grid.Columns = Math.Max(1, Math.Min(options.Count, maxColumns));
            foreach (var option in options)
            {
                grid.Children.Add(CreateChoice(option, title(option), withIcon, onClick));
            }
        }

        foreach (var button in grid.Children.OfType<RadioButton>())
        {
            button.IsChecked = button.Tag as string == selected;
        }
    }

    private static RadioButton CreateChoice(string option, string title, bool withIcon, EventHandler<RoutedEventArgs> onClick)
    {
        var label = new TextBlock
        {
            Text = title,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        object content = label;
        if (withIcon)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 3 };
            stack.Children.Add(new PathIcon { Data = ModeIcon(option), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(label);
            content = stack;
        }

        var button = new RadioButton { Content = content, Tag = option, MinHeight = withIcon ? 44 : 32 };
        button.Classes.Add("segment");
        AutomationProperties.SetName(button, title);
        button.IsCheckedChanged += onClick;
        return button;
    }

    private static Geometry? ModeIcon(string mode)
    {
        var key = mode switch
        {
            "COOL" => "Icon.ModeCool",
            "HEAT" => "Icon.ModeHeat",
            "AIR_DRY" => "Icon.ModeDry",
            "FAN" => "Icon.ModeFan",
            _ => "Icon.ModeAuto",
        };
        return Application.Current is { } application && application.TryGetResource(key, null, out var value)
            ? value as Geometry
            : null;
    }

    private void OnEffectivelyVisibleChanged(bool visible)
    {
        try
        {
            if (visible)
            {
                RenderFooter();
            }
            else
            {
                CloseMenu();
            }

            UpdateClock();
        }
        catch (Exception)
        {
        }
    }

    private void UpdateClock()
    {
        if (!disposed && visibility.IsVisible)
        {
            clock.Start();
        }
        else
        {
            clock.Stop();
        }
    }

    private void CloseMenu()
    {
        menus.Close();
    }

    private void OnMenuClosed(object? sender, EventArgs e)
    {
        PopupClosed?.Invoke(this, EventArgs.Empty);
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        TickClock();
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

    private async Task SendAsync(ACCommand command)
    {
        try
        {
            await Controller.SendAsync(command);
        }
        catch (Exception)
        {
        }

        SafeRender();
    }

    private async void OnPowerClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await SendAsync(new ACCommand.Power(Controller.State?.IsOn != true));
        }
        catch (Exception)
        {
        }
    }

    private void OnMinusClick(object? sender, RoutedEventArgs e)
    {
        StepTargetTemp(-1);
    }

    private void OnPlusClick(object? sender, RoutedEventArgs e)
    {
        StepTargetTemp(1);
    }

    private void StepTargetTemp(int steps)
    {
        try
        {
            Controller.StepTargetTemp(steps);
        }
        catch (Exception)
        {
        }
    }

    private async void OnModeChanged(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (rendering || sender is not RadioButton { IsChecked: true, Tag: string mode })
            {
                return;
            }

            if (Controller.State?.JobMode != mode)
            {
                await SendAsync(new ACCommand.JobMode(mode));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnWindChanged(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (rendering || sender is not RadioButton { IsChecked: true, Tag: string speed })
            {
                return;
            }

            if (Controller.State?.WindStrength != speed)
            {
                await SendAsync(new ACCommand.WindStrength(speed));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnSwingUpDownChanged(object? sender, RoutedEventArgs e)
    {
        try
        {
            var value = SwingUpDownToggle.IsChecked == true;
            if (!rendering && value != (Controller.State?.SwingUpDown ?? false))
            {
                await SendAsync(new ACCommand.SwingUpDown(value));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnSwingLeftRightChanged(object? sender, RoutedEventArgs e)
    {
        try
        {
            var value = SwingLeftRightToggle.IsChecked == true;
            if (!rendering && value != (Controller.State?.SwingLeftRight ?? false))
            {
                await SendAsync(new ACCommand.SwingLeftRight(value));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnDisplayLightChanged(object? sender, RoutedEventArgs e)
    {
        try
        {
            var value = DisplayLightToggle.IsChecked == true;
            if (!rendering && value != (Controller.State?.DisplayLightOn ?? false))
            {
                await SendAsync(new ACCommand.DisplayLight(value));
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnTimerClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            CloseMenu();
            var menu = new ContextMenu { Placement = PlacementMode.Bottom };
            menu.Items.Add(CreateTimerItem("Desativado", null));
            foreach (var minutes in TimerOptions)
            {
                menu.Items.Add(CreateTimerItem(ACLabels.Duration(minutes), minutes));
            }

            menus.Open(menu, TimerButton);
        }
        catch (Exception)
        {
        }
    }

    private MenuItem CreateTimerItem(string header, int? minutes)
    {
        var item = new MenuItem { Header = header };
        item.Click += async (_, _) =>
        {
            try
            {
                await SendAsync(new ACCommand.OffTimer(minutes));
            }
            catch (Exception)
            {
            }
        };
        return item;
    }

    private async void OnSwitchDeviceClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await Controller.ChooseAnotherDeviceAsync();
        }
        catch (Exception)
        {
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            await Controller.RefreshAsync(force: true);
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
            menu.Placement = PlacementMode.Top;
            menus.Open(menu, SettingsButton);
        }
        catch (Exception)
        {
        }
    }
}
