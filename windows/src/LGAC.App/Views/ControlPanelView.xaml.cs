using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LGAC.App.Platform;
using LGAC.Core;

namespace LGAC.App.Views;

public partial class ControlPanelView : UserControl
{
    private const string AccentBrushKey = "AccentBrush";
    private const string AccentForegroundBrushKey = "AccentForegroundBrush";
    private const string ControlBackgroundBrushKey = "ControlBackgroundBrush";
    private const string PanelForegroundBrushKey = "PanelForegroundBrush";
    private const string SegmentButtonStyleKey = "SegmentButton";
    private const double DimmedOpacity = 0.45;
    private const double GlyphSize = 16;
    private const double GlyphCenter = 8;

    private static readonly int[] TimerOptions = [30, 60, 120, 180, 240, 360, 480];

    private readonly ACController controller;
    private readonly StartupRegistration startup;
    private readonly DispatcherTimer clock;
    private ContextMenu? openMenu;
    private bool subscribed;
    private bool rendering;

    public ControlPanelView(ACController controller, StartupRegistration startup)
    {
        this.controller = controller;
        this.startup = startup;
        InitializeComponent();

        clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        clock.Tick += OnClockTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        Render();
    }

    internal static FrameworkElement CreateModeGlyph(string? mode)
    {
        var strokes = new GeometryGroup { FillRule = FillRule.Nonzero };
        var fills = new GeometryGroup { FillRule = FillRule.Nonzero };
        switch (mode)
        {
            case "COOL":
                AddSnowflake(strokes);
                break;
            case "HEAT":
                AddSun(strokes);
                break;
            case "AIR_DRY":
                AddDrop(fills);
                break;
            case "FAN":
                AddFan(fills);
                break;
            default:
                AddThermometer(strokes, fills);
                break;
        }

        strokes.Freeze();
        fills.Freeze();

        var canvas = new Canvas { Width = GlyphSize, Height = GlyphSize, IsHitTestVisible = false };
        canvas.Children.Add(CreateGlyphPath(strokes, stroked: true));
        canvas.Children.Add(CreateGlyphPath(fills, stroked: false));
        return canvas;
    }

    private void Render()
    {
        rendering = true;
        try
        {
            var caps = controller.Capabilities;
            var state = controller.State ?? new ACState();
            var isOn = state.IsOn == true;

            SwitchDeviceButton.Visibility = VisibleWhen(controller.CanSwitchDevice);
            AliasText.Text = string.IsNullOrEmpty(controller.DeviceAlias) ? "Ar-condicionado" : controller.DeviceAlias;
            if (state.CurrentTemp is { } current)
            {
                AmbientText.Text = "Ambiente " + ACLabels.Temperature(current);
                AmbientText.Visibility = Visibility.Visible;
            }
            else
            {
                AmbientText.Text = string.Empty;
                AmbientText.Visibility = Visibility.Collapsed;
            }

            var powerLabel = isOn ? "Desligar" : "Ligar";
            PowerButton.IsEnabled = caps?.Power == true;
            PowerButton.SetResourceReference(Control.BackgroundProperty, isOn ? AccentBrushKey : ControlBackgroundBrushKey);
            PowerButton.SetResourceReference(Control.ForegroundProperty, isOn ? AccentForegroundBrushKey : PanelForegroundBrushKey);
            PowerButton.ToolTip = powerLabel;
            AutomationProperties.SetName(PowerButton, powerLabel);

            Body.IsEnabled = isOn;
            Body.Opacity = isOn ? 1 : DimmedOpacity;

            TemperatureRow.Visibility = VisibleWhen(caps?.TargetTemp is not null);
            TargetTempText.Text = state.TargetTemp is { } target ? ACLabels.Temperature(target) : "–";

            RenderChoices(ModeSection, ModeGrid, caps?.JobModes ?? Array.Empty<string>(), state.JobMode, true, ACLabels.JobMode, OnModeClick);
            RenderChoices(WindSection, WindGrid, caps?.WindStrengths ?? Array.Empty<string>(), state.WindStrength, false, ACLabels.WindStrength, OnWindClick);

            SwingUpDownRow.Visibility = VisibleWhen(caps?.SwingUpDown == true);
            SwingUpDownToggle.IsChecked = state.SwingUpDown ?? false;
            SwingLeftRightRow.Visibility = VisibleWhen(caps?.SwingLeftRight == true);
            SwingLeftRightToggle.IsChecked = state.SwingLeftRight ?? false;
            DisplayLightRow.Visibility = VisibleWhen(caps?.DisplayLight is not null);
            DisplayLightToggle.IsChecked = state.DisplayLightOn ?? false;
            TimerRow.Visibility = VisibleWhen(caps?.OffTimer is not null);
            TimerText.Text = state.OffTimerRemainingMinutes is { } minutes ? ACLabels.Duration(minutes) : "Desativado";
            Switches.Visibility = VisibleWhen(
                SwingUpDownRow.Visibility == Visibility.Visible
                || SwingLeftRightRow.Visibility == Visibility.Visible
                || DisplayLightRow.Visibility == Visibility.Visible
                || TimerRow.Visibility == Visibility.Visible);

            RenderFooter();
        }
        finally
        {
            rendering = false;
        }
    }

    private void RenderFooter()
    {
        if (controller.StatusMessage is { } message)
        {
            StatusText.Text = message;
            StatusPanel.Visibility = Visibility.Visible;
            UpdatedText.Visibility = Visibility.Collapsed;
        }
        else if (controller.LastUpdated is { } updated)
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            UpdatedText.Text = "Atualizado há " + Elapsed(DateTimeOffset.UtcNow - updated);
            UpdatedText.Visibility = Visibility.Visible;
        }
        else
        {
            StatusPanel.Visibility = Visibility.Collapsed;
            UpdatedText.Visibility = Visibility.Collapsed;
        }
    }

    private static string Elapsed(TimeSpan elapsed)
    {
        var seconds = (int)Math.Max(0, Math.Floor(elapsed.TotalSeconds));
        return seconds < 60 ? $"{seconds} s" : ACLabels.Duration(seconds / 60);
    }

    private static Visibility VisibleWhen(bool visible)
    {
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void RenderChoices(
        FrameworkElement section,
        UniformGrid grid,
        IReadOnlyList<string> options,
        string? selected,
        bool withGlyph,
        Func<string, string> title,
        RoutedEventHandler onClick)
    {
        section.Visibility = VisibleWhen(options.Count > 0);
        var current = grid.Children.OfType<RadioButton>().Select(button => button.Tag as string).ToList();
        if (!current.SequenceEqual(options))
        {
            grid.Children.Clear();
            foreach (var option in options)
            {
                grid.Children.Add(CreateChoice(option, title(option), withGlyph, onClick));
            }
        }

        foreach (var button in grid.Children.OfType<RadioButton>())
        {
            button.IsChecked = button.Tag as string == selected;
        }
    }

    private static RadioButton CreateChoice(string option, string title, bool withGlyph, RoutedEventHandler onClick)
    {
        var label = new TextBlock
        {
            Text = title,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        object content = label;
        if (withGlyph)
        {
            var glyph = CreateModeGlyph(option);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.Margin = new Thickness(0, 0, 0, 3);
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(glyph);
            stack.Children.Add(label);
            content = stack;
        }

        var button = new RadioButton { Content = content, Tag = option, MinHeight = withGlyph ? 40 : 26 };
        button.SetResourceReference(FrameworkElement.StyleProperty, SegmentButtonStyleKey);
        AutomationProperties.SetName(button, title);
        button.Click += onClick;
        return button;
    }

    private static Path CreateGlyphPath(Geometry geometry, bool stroked)
    {
        var path = new Path { Data = geometry };
        var foreground = new Binding
        {
            RelativeSource = RelativeSource.Self,
            Path = new PropertyPath(TextElement.ForegroundProperty),
        };

        if (stroked)
        {
            path.StrokeThickness = 1.4;
            path.StrokeStartLineCap = PenLineCap.Round;
            path.StrokeEndLineCap = PenLineCap.Round;
            path.StrokeLineJoin = PenLineJoin.Round;
            path.SetBinding(Shape.StrokeProperty, foreground);
        }
        else
        {
            path.SetBinding(Shape.FillProperty, foreground);
        }

        return path;
    }

    private static Point Polar(double radius, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        return new Point(GlyphCenter + radius * Math.Cos(radians), GlyphCenter + radius * Math.Sin(radians));
    }

    private static void AddSnowflake(GeometryGroup strokes)
    {
        var center = new Point(GlyphCenter, GlyphCenter);
        for (var arm = 0; arm < 6; arm++)
        {
            var angle = arm * 60.0;
            strokes.Children.Add(new LineGeometry(center, Polar(6.6, angle)));
            var branchBase = Polar(4.4, angle);
            strokes.Children.Add(new LineGeometry(branchBase, Polar(4.4 + 2.0, angle + 38.0)));
            strokes.Children.Add(new LineGeometry(branchBase, Polar(4.4 + 2.0, angle - 38.0)));
        }
    }

    private static void AddSun(GeometryGroup strokes)
    {
        strokes.Children.Add(new EllipseGeometry(new Point(GlyphCenter, GlyphCenter), 2.9, 2.9));
        for (var ray = 0; ray < 8; ray++)
        {
            var angle = ray * 45.0;
            strokes.Children.Add(new LineGeometry(Polar(4.9, angle), Polar(7.1, angle)));
        }
    }

    private static void AddDrop(GeometryGroup fills)
    {
        var figure = new PathFigure { StartPoint = new Point(8, 1.4), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new BezierSegment(new Point(6.6, 5), new Point(3.4, 7), new Point(3.4, 10), true));
        figure.Segments.Add(new LineSegment(new Point(3.4, 10.2), true));
        figure.Segments.Add(new ArcSegment(new Point(12.6, 10.2), new Size(4.6, 4.6), 0, false, SweepDirection.Counterclockwise, true));
        figure.Segments.Add(new LineSegment(new Point(12.6, 10), true));
        figure.Segments.Add(new BezierSegment(new Point(12.6, 7), new Point(9.4, 5), new Point(8, 1.4), true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        fills.Children.Add(geometry);
    }

    private static void AddFan(GeometryGroup fills)
    {
        for (var blade = 0; blade < 3; blade++)
        {
            var rotation = new RotateTransform(blade * 120.0, GlyphCenter, GlyphCenter);
            fills.Children.Add(new EllipseGeometry(new Point(GlyphCenter, 4.2), 2.3, 3.2, rotation));
        }

        fills.Children.Add(new EllipseGeometry(new Point(GlyphCenter, GlyphCenter), 1.5, 1.5));
    }

    private static void AddThermometer(GeometryGroup strokes, GeometryGroup fills)
    {
        var stem = new PathFigure { StartPoint = new Point(6.3, 3.3), IsClosed = true, IsFilled = false };
        stem.Segments.Add(new ArcSegment(new Point(9.7, 3.3), new Size(1.7, 1.7), 0, false, SweepDirection.Clockwise, true));
        stem.Segments.Add(new LineSegment(new Point(9.7, 9.4), true));
        stem.Segments.Add(new LineSegment(new Point(6.3, 9.4), true));
        var geometry = new PathGeometry();
        geometry.Figures.Add(stem);
        strokes.Children.Add(geometry);

        fills.Children.Add(new EllipseGeometry(new Point(GlyphCenter, 11.2), 3.1, 3.1));
        fills.Children.Add(new RectangleGeometry(new Rect(7.3, 6.0, 1.4, 5.0)));
    }

    private void CloseMenu()
    {
        if (openMenu is { IsOpen: true } menu)
        {
            menu.IsOpen = false;
        }

        openMenu = null;
    }

    private void UpdateClock()
    {
        if (IsVisible && subscribed)
        {
            clock.Start();
        }
        else
        {
            clock.Stop();
        }
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

    private async Task SendAsync(ACCommand command)
    {
        try
        {
            await controller.SendAsync(command);
        }
        catch (Exception)
        {
        }

        SafeRender();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!subscribed)
        {
            controller.PropertyChanged += OnControllerPropertyChanged;
            subscribed = true;
        }

        SafeRender();
        UpdateClock();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (subscribed)
        {
            controller.PropertyChanged -= OnControllerPropertyChanged;
            subscribed = false;
        }

        clock.Stop();
        CloseMenu();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            if (IsVisible)
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

    private void OnClockTick(object? sender, EventArgs e)
    {
        try
        {
            RenderFooter();
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

    private async void OnPowerClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!rendering)
            {
                await SendAsync(new ACCommand.Power(controller.State?.IsOn != true));
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnMinusClick(object sender, RoutedEventArgs e)
    {
        StepTargetTemp(-1);
    }

    private void OnPlusClick(object sender, RoutedEventArgs e)
    {
        StepTargetTemp(1);
    }

    private void StepTargetTemp(int steps)
    {
        try
        {
            if (!rendering)
            {
                controller.StepTargetTemp(steps);
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnModeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!rendering && sender is FrameworkElement { Tag: string mode })
            {
                await SendAsync(new ACCommand.JobMode(mode));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnWindClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!rendering && sender is FrameworkElement { Tag: string speed })
            {
                await SendAsync(new ACCommand.WindStrength(speed));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnSwingUpDownClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!rendering)
            {
                await SendAsync(new ACCommand.SwingUpDown(SwingUpDownToggle.IsChecked == true));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnSwingLeftRightClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!rendering)
            {
                await SendAsync(new ACCommand.SwingLeftRight(SwingLeftRightToggle.IsChecked == true));
            }
        }
        catch (Exception)
        {
        }
    }

    private async void OnDisplayLightClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!rendering)
            {
                await SendAsync(new ACCommand.DisplayLight(DisplayLightToggle.IsChecked == true));
            }
        }
        catch (Exception)
        {
        }
    }

    private void OnTimerClick(object sender, RoutedEventArgs e)
    {
        try
        {
            CloseMenu();
            var menu = new ContextMenu { PlacementTarget = TimerButton, Placement = PlacementMode.Bottom };
            menu.Items.Add(CreateTimerItem("Desativado", null));
            foreach (var minutes in TimerOptions)
            {
                menu.Items.Add(CreateTimerItem(ACLabels.Duration(minutes), minutes));
            }

            openMenu = menu;
            menu.IsOpen = true;
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

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await controller.RefreshAsync(force: true);
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
            openMenu = SettingsMenu.Open(SettingsButton, controller, startup, PlacementMode.Top);
        }
        catch (Exception)
        {
        }
    }
}
