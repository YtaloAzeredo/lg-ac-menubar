using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class ControlPanelViewTests
{
    private static readonly TimeSpan Elapsed = TimeSpan.FromSeconds(12);

    private static JsonNode FullOffState()
    {
        var state = Fixture.Json("state-full")!;
        state["operation"]!["airConOperationMode"] = "POWER_OFF";
        return state;
    }

    private static void UseBasicModel(PanelFixture fixture)
    {
        fixture.Client.SetProfile(Fixture.Json("profile-basic"));
        fixture.Client.SetState(Fixture.Json("state-basic"));
    }

    private static List<RadioButton> Segments(UniformGrid grid)
    {
        return grid.Children.OfType<RadioButton>().ToList();
    }

    private static RadioButton Segment(UniformGrid grid, string tag)
    {
        return Segments(grid).Single(button => (string?)button.Tag == tag);
    }

    private static string? Checked(UniformGrid grid)
    {
        return Segments(grid).SingleOrDefault(button => button.IsChecked == true)?.Tag as string;
    }

    private static MenuItem Item(ContextMenu menu, string header)
    {
        return menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header);
    }

    private static void AssertControls(PanelFixture fixture, params string[] expected)
    {
        Assert.HasCount(expected.Length, fixture.Client.Controls);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.IsTrue(
                JsonNode.DeepEquals(JsonNode.Parse(expected[index]), fixture.Client.Controls[index]),
                $"payload {index} diferente: {fixture.Client.Controls[index].ToJsonString()}");
        }
    }

    private static async Task<(ControlPanelView View, Window Window)> OpenAsync(PanelFixture fixture, Func<DateTimeOffset>? now = null)
    {
        await fixture.ToReadyAsync();
        var view = new ControlPanelView(fixture.Services, now);
        var window = UiTesting.Host(view);
        return (view, window);
    }

    [TestMethod]
    public async Task HeaderShowsAliasAmbientAndSwitchArrow()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Assert.AreEqual("Ar do Quarto", view.AliasText.Text);
            Assert.AreEqual("Ambiente 25°", view.AmbientText.Text);
            Assert.IsTrue(view.AmbientText.IsVisible);
            Assert.IsTrue(view.SwitchDeviceButton.IsVisible);
            Assert.IsTrue(view.PowerButton.IsEnabled);
            window.Close();
        });
    }

    [TestMethod]
    public async Task WithoutStateOrCapabilitiesRendersEmptyAndDoesNotThrow()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            using var view = new ControlPanelView(fixture.Services);
            var window = UiTesting.Host(view);

            Assert.IsNull(fixture.Controller.State);
            Assert.IsNull(fixture.Controller.Capabilities);
            Assert.AreEqual("Ar-condicionado", view.AliasText.Text);
            Assert.IsFalse(view.AmbientText.IsVisible);
            Assert.IsFalse(view.SwitchDeviceButton.IsVisible);
            Assert.IsFalse(view.PowerButton.IsEnabled);
            Assert.IsFalse(view.Body.IsEnabled);
            Assert.IsFalse(view.ModeSection.IsVisible);
            Assert.IsFalse(view.WindSection.IsVisible);
            Assert.IsFalse(view.SwingUpDownRow.IsVisible);
            Assert.IsFalse(view.TimerRow.IsVisible);
            Assert.IsFalse(view.StatusPanel.IsVisible);
            Assert.IsFalse(view.UpdatedText.IsVisible);
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ModeAndWindSegmentsShowLabelsAndMarkTheCurrentValue()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Assert.AreEqual("23°", view.TargetTempText.Text);
            CollectionAssert.AreEqual(
                new[] { "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO" },
                Segments(view.ModeGrid).Select(button => (string)button.Tag!).ToArray());
            CollectionAssert.AreEqual(
                new[] { "LOW", "MID", "HIGH", "AUTO" },
                Segments(view.WindGrid).Select(button => (string)button.Tag!).ToArray());
            var texts = UiTesting.VisibleTexts(view);
            CollectionAssert.IsSubsetOf(
                new[] { "Modo", "Frio", "Quente", "Seco", "Ventilar", "Auto", "Ventilador", "Baixa", "Média", "Alta" },
                texts.ToList());
            Assert.AreEqual("COOL", Checked(view.ModeGrid));
            Assert.AreEqual("MID", Checked(view.WindGrid));
            window.Close();
        });
    }

    [TestMethod]
    public async Task FullModelShowsSwitchesAndTimerWithTheCurrentValues()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            var texts = UiTesting.VisibleTexts(view);
            CollectionAssert.IsSubsetOf(
                new[] { "Swing vertical", "Swing horizontal", "Luz do visor", "Desligar em", "1h 20min" },
                texts.ToList());
            Assert.IsFalse(view.SwingUpDownToggle.IsChecked);
            Assert.IsTrue(view.SwingLeftRightToggle.IsChecked);
            Assert.IsTrue(view.DisplayLightToggle.IsChecked);
            Assert.AreEqual("1h 20min", view.TimerText.Text);
            window.Close();
        });
    }

    [TestMethod]
    public async Task TimerWithoutValueShowsDisabled()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var state = Fixture.Json("state-full")!;
            state["timer"]!["relativeStopTimer"] = "UNSET";
            fixture.Client.SetState(state);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Assert.AreEqual("Desativado", view.TimerText.Text);
            window.Close();
        });
    }

    [TestMethod]
    public async Task BasicModelHidesWhatItDoesNotSupport()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            UseBasicModel(fixture);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Assert.AreEqual("22,5°", view.TargetTempText.Text);
            Assert.AreEqual("Ambiente 27,5°", view.AmbientText.Text);
            Assert.IsTrue(view.SwingUpDownRow.IsVisible);
            Assert.IsTrue(view.SwingUpDownToggle.IsChecked);
            Assert.IsFalse(view.SwingLeftRightRow.IsVisible);
            Assert.IsFalse(view.DisplayLightRow.IsVisible);
            Assert.IsFalse(view.TimerRow.IsVisible);
            CollectionAssert.AreEqual(new[] { "COOL", "FAN" }, Segments(view.ModeGrid).Select(button => (string)button.Tag!).ToArray());
            CollectionAssert.AreEqual(new[] { "LOW", "HIGH" }, Segments(view.WindGrid).Select(button => (string)button.Tag!).ToArray());
            Assert.AreEqual("FAN", Checked(view.ModeGrid));
            Assert.AreEqual("HIGH", Checked(view.WindGrid));
            window.Close();
        });
    }

    [TestMethod]
    public async Task SwitchesSectionDisappearsWhenNothingInItIsSupported()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            UseBasicModel(fixture);
            var profile = Fixture.Json("profile-basic")!;
            profile["property"]!.AsObject().Remove("windDirection");
            fixture.Client.SetProfile(profile);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Assert.IsFalse(view.Switches.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task PowerButtonTurnsOffWhenOnAndOnWhenOff()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using (var fixture = PanelFixture.Create(configured: true))
            {
                var (view, window) = await OpenAsync(fixture);
                using var _ = view;
                Assert.IsTrue(view.PowerButton.Classes.Contains("on"));

                UiTesting.Click(window, view.PowerButton);
                await PanelFixture.SettleAsync();

                AssertControls(fixture, """{"operation":{"airConOperationMode":"POWER_OFF"}}""");
                window.Close();
            }

            using (var fixture = PanelFixture.Create(configured: true))
            {
                fixture.Client.SetState(FullOffState());
                var (view, window) = await OpenAsync(fixture);
                using var _ = view;
                Assert.IsFalse(view.PowerButton.Classes.Contains("on"));

                UiTesting.Click(window, view.PowerButton);
                await PanelFixture.SettleAsync();

                AssertControls(fixture, """{"operation":{"airConOperationMode":"POWER_ON"}}""");
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task ModeSegmentSendsTheMode()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, Segment(view.ModeGrid, "HEAT"));
            await PanelFixture.SettleAsync();

            AssertControls(fixture, """{"airConJobMode":{"currentJobMode":"HEAT"}}""");
            window.Close();
        });
    }

    [TestMethod]
    public async Task WindSegmentSendsTheStrength()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, Segment(view.WindGrid, "HIGH"));
            await PanelFixture.SettleAsync();

            AssertControls(fixture, """{"airFlow":{"windStrength":"HIGH"}}""");
            window.Close();
        });
    }

    [TestMethod]
    public async Task SelectedSegmentFollowsTheNewValueImmediately()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            fixture.Client.HoldNextState();

            UiTesting.Click(window, Segment(view.ModeGrid, "HEAT"));

            Assert.AreEqual("HEAT", Checked(view.ModeGrid));
            Assert.IsFalse(Segment(view.ModeGrid, "COOL").IsChecked);
            fixture.Client.ReleaseState();
            window.Close();
            await PanelFixture.SettleAsync();
        });
    }

    [TestMethod]
    public async Task SwitchesSendTheirCommands()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, view.SwingUpDownToggle);
            await PanelFixture.SettleAsync();
            UiTesting.Click(window, view.SwingLeftRightToggle);
            await PanelFixture.SettleAsync();
            UiTesting.Click(window, view.DisplayLightToggle);
            await PanelFixture.SettleAsync();

            AssertControls(
                fixture,
                """{"windDirection":{"rotateUpDown":true}}""",
                """{"windDirection":{"rotateLeftRight":false}}""",
                """{"display":{"light":"OFF"}}""");
            window.Close();
        });
    }

    [TestMethod]
    public async Task TimerMenuOffersDisabledAndTheSevenDurations()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, view.TimerButton);

            var menu = view.OpenMenu;
            Assert.IsNotNull(menu);
            CollectionAssert.AreEqual(
                new[] { "Desativado", "30 min", "1 h", "2 h", "3 h", "4 h", "6 h", "8 h" },
                menu.Items.OfType<MenuItem>().Select(item => (string)item.Header!).ToArray());
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task TimerMenuSendsTheChosenDuration()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, view.TimerButton);
            Item(view.OpenMenu!, "2 h").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await PanelFixture.SettleAsync();
            UiTesting.Click(window, view.TimerButton);
            Item(view.OpenMenu!, "30 min").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await PanelFixture.SettleAsync();

            AssertControls(fixture, """{"timer":{"relativeHourToStop":2}}""", """{"timer":{"relativeHourToStop":0,"relativeMinuteToStop":30}}""");
            window.Close();
        });
    }

    [TestMethod]
    public async Task TimerMenuDisabledSendsUnset()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, view.TimerButton);
            Item(view.OpenMenu!, "Desativado").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await PanelFixture.SettleAsync();

            AssertControls(fixture, """{"timer":{"relativeStopTimer":"UNSET"}}""");
            window.Close();
        });
    }

    [TestMethod]
    public async Task MinusAndPlusStepTheTargetAndShowItAtOnce()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            fixture.Client.HoldNextState();

            UiTesting.Click(window, view.PlusButton);
            UiTesting.Click(window, view.PlusButton);
            Assert.AreEqual("25°", view.TargetTempText.Text);
            UiTesting.Click(window, view.MinusButton);
            Assert.AreEqual("24°", view.TargetTempText.Text);

            await PanelFixture.SettleAsync();

            Assert.IsNotEmpty(fixture.Client.Controls);
            Assert.IsTrue(
                JsonNode.DeepEquals(JsonNode.Parse("""{"temperature":{"targetTemperature":24}}"""), fixture.Client.Controls[^1]),
                $"último payload diferente: {fixture.Client.Controls[^1].ToJsonString()}");
            fixture.Client.ReleaseState();
            fixture.Client.ReleaseState();
            window.Close();
        });
    }

    [TestMethod]
    public async Task TemperatureUsesTheModelStep()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            UseBasicModel(fixture);
            var state = Fixture.Json("state-basic")!;
            state["operation"]!["airConOperationMode"] = "POWER_ON";
            fixture.Client.SetState(state);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            fixture.Client.HoldNextState();

            UiTesting.Click(window, view.PlusButton);

            Assert.AreEqual("23°", view.TargetTempText.Text);
            fixture.Client.ReleaseState();
            window.Close();
            await PanelFixture.SettleAsync();
        });
    }

    [TestMethod]
    public async Task OffDeviceDisablesTheBodyButKeepsTheRest()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            fixture.Client.SetState(FullOffState());
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Assert.IsFalse(view.Body.IsEnabled);
            Assert.IsLessThan(1.0, view.Body.Opacity);
            Assert.IsTrue(view.PowerButton.IsEnabled);
            Assert.IsTrue(view.SwitchDeviceButton.IsEnabled);
            Assert.IsTrue(view.RefreshButton.IsEnabled);
            Assert.IsTrue(view.SettingsButton.IsEnabled);
            Assert.IsFalse(view.PlusButton.IsEffectivelyEnabled);
            Assert.IsFalse(view.TimerButton.IsEffectivelyEnabled);

            UiTesting.Click(window, view.PlusButton);
            UiTesting.Click(window, Segment(view.ModeGrid, "HEAT"));
            await PanelFixture.SettleAsync();
            Assert.IsEmpty(fixture.Client.Controls);
            window.Close();
        });
    }

    [TestMethod]
    public async Task TurningOnReenablesTheBody()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            fixture.Client.SetState(FullOffState());
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            fixture.Client.HoldNextState();

            UiTesting.Click(window, view.PowerButton);

            Assert.IsTrue(view.Body.IsEnabled);
            Assert.AreEqual(1.0, view.Body.Opacity);
            fixture.Client.ReleaseState();
            window.Close();
            await PanelFixture.SettleAsync();
        });
    }

    [TestMethod]
    public async Task RenderDoesNotSendCommands()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            var changed = Fixture.Json("state-full")!;
            changed["airConJobMode"]!["currentJobMode"] = "HEAT";
            changed["airFlow"]!["windStrength"] = "HIGH";
            changed["windDirection"]!["rotateUpDown"] = true;
            changed["windDirection"]!["rotateLeftRight"] = false;
            changed["display"]!["light"] = "OFF";
            changed["timer"]!["relativeStopTimer"] = "UNSET";
            fixture.Client.SetState(changed);
            await fixture.Controller.RefreshAsync(force: true);
            await PanelFixture.SettleAsync();

            Assert.AreEqual("HEAT", Checked(view.ModeGrid));
            Assert.AreEqual("HIGH", Checked(view.WindGrid));
            Assert.IsTrue(view.SwingUpDownToggle.IsChecked);
            Assert.IsFalse(view.SwingLeftRightToggle.IsChecked);
            Assert.IsFalse(view.DisplayLightToggle.IsChecked);
            Assert.AreEqual("Desativado", view.TimerText.Text);
            Assert.IsEmpty(fixture.Client.Controls);
            window.Close();
        });
    }

    [TestMethod]
    public async Task RefreshAndSwitchDeviceAndSettingsButtonsWork()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            var before = fixture.Client.StateCalls;

            UiTesting.Click(window, view.RefreshButton);
            await PanelFixture.SettleAsync();
            Assert.AreEqual(before + 1, fixture.Client.StateCalls);

            UiTesting.Click(window, view.SettingsButton);
            Assert.IsNotNull(view.OpenMenu);
            CollectionAssert.Contains(
                view.OpenMenu.Items.OfType<MenuItem>().Select(item => (string?)item.Header).ToList(),
                "Sair");

            UiTesting.Click(window, view.SwitchDeviceButton);
            await PanelFixture.SettleAsync();
            Assert.IsInstanceOfType<ACPhase.ChoosingDevice>(fixture.Controller.Phase);
            window.Close();
        });
    }

    [TestMethod]
    public async Task FooterShowsTheStatusMessageInsteadOfTheElapsedTime()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            Assert.IsFalse(view.StatusPanel.IsVisible);
            Assert.IsTrue(view.UpdatedText.IsVisible);

            fixture.Client.SetControlError(new ThinQException(ThinQErrorKind.CommandRejected, "2305", "Modo não permite"));
            await fixture.Controller.SendAsync(new ACCommand.JobMode("HEAT"));
            await PanelFixture.SettleAsync();

            Assert.IsTrue(view.StatusPanel.IsVisible);
            Assert.AreEqual("Comando recusado: Modo não permite", view.StatusText.Text);
            Assert.IsFalse(view.UpdatedText.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task FooterShowsTheElapsedTimeFromTheInjectedClock()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var now = fixture.Clock.Date + Elapsed;
            var (view, window) = await OpenAsync(fixture, () => now);
            using var _ = view;

            Assert.IsTrue(view.UpdatedText.IsVisible);
            Assert.AreEqual("Atualizado há 12 seg", view.UpdatedText.Text);
            Assert.IsFalse(view.StatusPanel.IsVisible);
            window.Close();
        });
    }

    [TestMethod]
    public async Task FooterAdvancesWhenTheClockTicks()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var now = fixture.Clock.Date + Elapsed;
            var (view, window) = await OpenAsync(fixture, () => now);
            using var _ = view;
            Assert.AreEqual("Atualizado há 12 seg", view.UpdatedText.Text);

            now = fixture.Clock.Date + TimeSpan.FromSeconds(75);
            Assert.AreEqual("Atualizado há 12 seg", view.UpdatedText.Text);
            view.TickClock();

            Assert.AreEqual("Atualizado há 1 min e 15 seg", view.UpdatedText.Text);
            window.Close();
        });
    }

    [TestMethod]
    public async Task ClockRunsOnlyWhileTheViewIsVisible()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var now = fixture.Clock.Date + Elapsed;
            await fixture.ToReadyAsync();
            var view = new ControlPanelView(fixture.Services, () => now);
            Assert.IsFalse(view.IsClockRunning);

            var window = UiTesting.Host(view);
            Assert.IsTrue(view.IsClockRunning);

            window.Hide();
            Assert.IsFalse(view.IsClockRunning);

            now = fixture.Clock.Date + TimeSpan.FromSeconds(30);
            window.Show();
            Assert.IsTrue(view.IsClockRunning);
            Assert.AreEqual("Atualizado há 30 seg", view.UpdatedText.Text);

            view.Dispose();
            Assert.IsFalse(view.IsClockRunning);
            window.Close();
        });
    }

    [TestMethod]
    public async Task StopsReactingAfterDispose()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            view.Dispose();
            view.Dispose();

            var changed = Fixture.Json("state-full")!;
            changed["airConJobMode"]!["currentJobMode"] = "HEAT";
            fixture.Client.SetState(changed);
            await fixture.Controller.RefreshAsync(force: true);
            await PanelFixture.SettleAsync();

            Assert.AreEqual("COOL", Checked(view.ModeGrid));
            window.Close();
        });
    }

    [TestMethod]
    public async Task CapturesTheModels()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using (var fixture = PanelFixture.Create(configured: true))
            {
                var (view, window) = await OpenAsync(fixture, () => fixture.Clock.Date + Elapsed);
                UiTesting.CaptureBoth(window, "control-full-on");
                UiTesting.Click(window, view.TimerButton);
                await PanelFixture.SettleAsync();
                UiTesting.CaptureBoth(window, "control-timer-menu");
                view.Dispose();
                window.Close();
            }

            using (var fixture = PanelFixture.Create(configured: true))
            {
                fixture.Client.SetState(FullOffState());
                var (view, window) = await OpenAsync(fixture, () => fixture.Clock.Date + Elapsed);
                UiTesting.CaptureBoth(window, "control-full-off");
                view.Dispose();
                window.Close();
            }

            using (var fixture = PanelFixture.Create(configured: true))
            {
                UseBasicModel(fixture);
                var state = Fixture.Json("state-basic")!;
                state["operation"]!["airConOperationMode"] = "POWER_ON";
                fixture.Client.SetState(state);
                var (view, window) = await OpenAsync(fixture, () => fixture.Clock.Date + Elapsed);
                UiTesting.CaptureBoth(window, "control-basic");
                view.Dispose();
                window.Close();
            }

            using (var fixture = PanelFixture.Create(configured: true))
            {
                var (view, window) = await OpenAsync(fixture);
                fixture.Client.SetControlError(new ThinQException(ThinQErrorKind.CommandRejected, "2305", "Modo não permite"));
                await fixture.Controller.SendAsync(new ACCommand.JobMode("HEAT"));
                await PanelFixture.SettleAsync();
                UiTesting.CaptureBoth(window, "control-status-message");
                view.Dispose();
                window.Close();
            }
        });
    }

    [TestMethod]
    public async Task SettingsAndTimerMenusCountAsOpenPopups()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            var closed = 0;
            view.PopupClosed += (_, _) => closed++;
            Assert.IsFalse(view.HasOpenPopup);

            UiTesting.Click(window, view.SettingsButton);
            Assert.IsTrue(view.HasOpenPopup);
            view.OpenMenu!.Close();
            Assert.IsFalse(view.HasOpenPopup);
            Assert.AreEqual(1, closed);

            UiTesting.Click(window, view.TimerButton);
            Assert.IsTrue(view.HasOpenPopup);
            view.OpenMenu!.Close();
            Assert.AreEqual(2, closed);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task DraggingASwitchSendsTheCommand()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            view.SwingUpDownToggle.IsChecked = true;
            await PanelFixture.SettleAsync();

            AssertControls(fixture, """{"windDirection":{"rotateUpDown":true}}""");
            window.Close();
        });
    }

    [TestMethod]
    public async Task SegmentCheckedWithoutAClickSendsTheMode()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            Segment(view.ModeGrid, "HEAT").IsChecked = true;
            await PanelFixture.SettleAsync();

            Assert.HasCount(1, fixture.Client.Controls);
            window.Close();
        });
    }

    [TestMethod]
    public async Task ClickSendsExactlyOneCommand()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;

            UiTesting.Click(window, view.SwingUpDownToggle);
            await PanelFixture.SettleAsync();
            Assert.HasCount(1, fixture.Client.Controls);

            UiTesting.Click(window, Segment(view.ModeGrid, "HEAT"));
            await PanelFixture.SettleAsync();
            Assert.HasCount(2, fixture.Client.Controls);
            window.Close();
        });
    }

    [TestMethod]
    public async Task RejectedCommandsReturnTheViewToTheRealState()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var (view, window) = await OpenAsync(fixture);
            using var _ = view;
            fixture.Client.SetControlError(new ThinQException(ThinQErrorKind.CommandRejected, "2305", "Modo não permite"));

            UiTesting.Click(window, Segment(view.ModeGrid, "HEAT"));
            await PanelFixture.SettleAsync();
            UiTesting.Click(window, view.SwingUpDownToggle);
            await PanelFixture.SettleAsync();

            Assert.AreEqual("COOL", Checked(view.ModeGrid));
            Assert.IsFalse(view.SwingUpDownToggle.IsChecked);
            Assert.IsTrue(view.StatusPanel.IsVisible);
            window.Close();
        });
    }
}
