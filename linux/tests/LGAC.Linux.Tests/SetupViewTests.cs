using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class SetupViewTests
{
    private static readonly Uri TokenPage = new("https://connect-pat.lgthinq.com");

    private static (SetupView View, Window Window, List<Uri> Opened) Show(PanelFixture fixture, string? message = null)
    {
        var opened = new List<Uri>();
        var view = new SetupView(fixture.Services, message, uri =>
        {
            opened.Add(uri);
            return Task.CompletedTask;
        });
        return (view, UiTesting.Host(view), opened);
    }

    [TestMethod]
    public async Task ShowsTheSetupTexts()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToSetupAsync();
            var (view, window, _) = Show(fixture);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Conectar ao LG ThinQ",
                    "Gere um Personal Access Token com permissão para ver e controlar aparelhos e cole abaixo.",
                    "País",
                    "Brasil",
                    "Token",
                    "Gerar token",
                    "Conectar",
                    "Sair",
                },
                UiTesting.VisibleTexts(view).ToList());
            Assert.AreEqual("Token", view.TokenBox.PlaceholderText);
            Assert.AreEqual('•', view.TokenBox.PasswordChar);
            Assert.IsTrue(view.ConnectButton.IsDefault);
            window.Close();
        });
    }

    [TestMethod]
    public async Task ConnectIsDisabledUntilTheTokenHasText()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToSetupAsync();
            var (view, window, _) = Show(fixture);

            Assert.IsFalse(view.ConnectButton.IsEnabled);
            view.TokenBox.Focus();
            window.KeyTextInput("abc");
            Assert.AreEqual("abc", view.TokenBox.Text);
            Assert.IsTrue(view.ConnectButton.IsEnabled);

            view.TokenBox.Text = "";
            Assert.IsFalse(view.ConnectButton.IsEnabled);
            window.Close();
        });
    }

    [TestMethod]
    public async Task ConnectSendsTheTypedTokenAndChosenCountryAndClearsTheField()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            fixture.Client.SetDevices([TestDevices.Ac1]);
            await fixture.ToSetupAsync();
            var (view, window, _) = Show(fixture);

            view.TokenBox.Focus();
            window.KeyTextInput("meu-token");
            view.CountryBox.SelectedItem = view.CountryBox.Items
                .OfType<ComboBoxItem>()
                .Single(item => (string?)item.Tag == "CL");
            UiTesting.Click(window, view.ConnectButton);

            Assert.AreEqual(string.Empty, view.TokenBox.Text);
            await PanelFixture.SettleAsync();

            Assert.AreEqual(new ThinQCredentials("meu-token", "CL", "lgac-test-client"), fixture.Credentials.All.First());
            Assert.AreEqual("meu-token", fixture.Store.Token);
            Assert.AreEqual("CL", fixture.Store.Country);
            Assert.AreEqual(new ACPhase.Ready(), fixture.Controller.Phase);
            window.Close();
        });
    }

    [TestMethod]
    public async Task BrazilIsTheDefaultCountry()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            await fixture.ToSetupAsync();
            var (view, window, _) = Show(fixture);

            var selected = (ComboBoxItem)view.CountryBox.SelectedItem!;
            Assert.AreEqual("BR", selected.Tag);
            Assert.AreEqual("Brasil", selected.Content);
            window.Close();

            fixture.Store.Country = "AR";
            var (other, otherWindow, _) = Show(fixture);
            Assert.AreEqual("Argentina", ((ComboBoxItem)other.CountryBox.SelectedItem!).Content);
            otherWindow.Close();
        });
    }

    [TestMethod]
    public async Task UnknownStoredCountryFallsBackToBrazil()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            fixture.Store.Country = "ZZ";
            var (view, window, _) = Show(fixture);

            Assert.AreEqual("BR", ((ComboBoxItem)view.CountryBox.SelectedItem!).Tag);
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ShowsTheErrorMessageOnlyWhenThereIsOne()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var (clean, cleanWindow, _) = Show(fixture);
            Assert.IsFalse(clean.MessageText.IsVisible);
            cleanWindow.Close();

            var (view, window, _) = Show(fixture, "Token inválido");
            Assert.IsTrue(view.MessageText.IsVisible);
            Assert.AreEqual("Token inválido", view.MessageText.Text);
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task QuitCallsTheQuitAction()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var (view, window, _) = Show(fixture);

            UiTesting.Click(window, view.QuitButton);

            Assert.AreEqual(1, fixture.QuitCount);
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task GenerateTokenOpensThePatPage()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var (view, window, opened) = Show(fixture);

            UiTesting.Click(window, view.GenerateTokenButton);
            await PanelFixture.SettleAsync();

            CollectionAssert.AreEqual(new[] { TokenPage }, opened);
            window.Close();
        });
    }

    [TestMethod]
    public async Task FailingOpenerDoesNotBreakTheView()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var view = new SetupView(fixture.Services, null, _ => throw new InvalidOperationException("sem navegador"));
            var window = UiTesting.Host(view);

            UiTesting.Click(window, view.GenerateTokenButton);
            await PanelFixture.SettleAsync();

            Assert.IsTrue(view.GenerateTokenButton.IsEnabled);
            window.Close();
        });
    }

    [TestMethod]
    public async Task CapturesTheSetupScreens()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var (view, window, _) = Show(fixture);
            UiTesting.CaptureBoth(window, "setup");
            view.TokenBox.Focus();
            window.KeyTextInput("token-secreto-12345");
            UiTesting.CaptureBoth(window, "setup-typed");
            window.Close();

            var (errorView, errorWindow, _) = Show(fixture, "Token inválido ou sem permissão. Gere um novo token e tente de novo.");
            UiTesting.CaptureBoth(errorWindow, "setup-error");
            errorWindow.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task CountryDropdownCountsAsAnOpenPopup()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            var (view, window, _) = Show(fixture);
            var closed = 0;
            view.PopupClosed += (_, _) => closed++;
            Assert.IsFalse(view.HasOpenPopup);

            view.CountryBox.IsDropDownOpen = true;
            Assert.IsTrue(view.HasOpenPopup);
            view.CountryBox.IsDropDownOpen = false;

            Assert.IsFalse(view.HasOpenPopup);
            Assert.AreEqual(1, closed);
            window.Close();
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task UndoAfterConnectDoesNotBringTheTokenBack()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create();
            fixture.Client.SetDevices([TestDevices.Ac1]);
            await fixture.ToSetupAsync();
            var (view, window, _) = Show(fixture);
            view.TokenBox.Focus();
            window.KeyTextInput("meu-token");

            UiTesting.Click(window, view.ConnectButton);
            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, null);
            await PanelFixture.SettleAsync();

            Assert.AreEqual(string.Empty, view.TokenBox.Text);
            Assert.IsFalse(view.TokenBox.IsUndoEnabled);
            window.Close();
        });
    }

    private sealed class ThrowingLauncher : ILauncher
    {
        public Task<bool> LaunchUriAsync(Uri uri) => throw new InvalidOperationException("sem portal");

        public Task<bool> LaunchFileAsync(IStorageItem storageItem) => Task.FromResult(false);
    }

    private sealed class FalseLauncher : ILauncher
    {
        public Task<bool> LaunchUriAsync(Uri uri) => Task.FromResult(false);

        public Task<bool> LaunchFileAsync(IStorageItem storageItem) => Task.FromResult(false);
    }

    [TestMethod]
    public async Task OpenerFallsBackToXdgOpenWhenTheLauncherThrowsOrReturnsFalse()
    {
        var fallbacks = new List<Uri>();
        Task Record(Uri uri)
        {
            fallbacks.Add(uri);
            return Task.CompletedTask;
        }

        await UriOpener.OpenAsync(new ThrowingLauncher(), TokenPage, Record);
        await UriOpener.OpenAsync(new FalseLauncher(), TokenPage, Record);
        await UriOpener.OpenAsync(null, TokenPage, Record);

        Assert.HasCount(3, fallbacks);
    }

    [TestMethod]
    public async Task FailingFallbackDoesNotEscapeTheOpener()
    {
        await UriOpener.OpenAsync(new ThrowingLauncher(), TokenPage, _ => throw new System.ComponentModel.Win32Exception("xdg-open ausente"));
        await UriOpener.OpenAsync(null, TokenPage, _ => Task.FromException(new InvalidOperationException("falhou")));
    }
}
