using Avalonia.Threading;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Platform;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests.Support;

internal sealed class PanelFixture : IDisposable
{
    private readonly string directory;
    private readonly List<PanelWindow> panels = [];

    private PanelFixture(InMemoryCredentialStore store)
    {
        directory = Path.Combine(Path.GetTempPath(), "lgac-panel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        AutostartPath = Path.Combine(directory, "autostart", "lg-ac.desktop");
        Client = new FakeClient(Fixture.Json("profile-full"), Fixture.Json("state-full"), [TestDevices.Ac1, TestDevices.Ac2]);
        Store = store;
        Clock = new TestNow();
        Credentials = new CredentialsLog();
        Controller = ControllerHarness.MakeController(Client, store, Clock, Credentials);
        Autostart = new AutostartEntry(AutostartPath, "/opt/lg-ac/lg-ac");
        Services = new PanelServices(Controller, store, Autostart, () => QuitCount++);
    }

    public FakeClient Client { get; }

    public InMemoryCredentialStore Store { get; }

    public TestNow Clock { get; }

    public CredentialsLog Credentials { get; }

    public ACController Controller { get; }

    public AutostartEntry Autostart { get; }

    public string AutostartPath { get; }

    public int QuitCount { get; private set; }

    public PanelServices Services { get; }

    public PanelWindow Panel(bool trayMode, Func<long>? tickClock = null, Func<bool>? isActive = null)
    {
        var window = new PanelWindow(Services, trayMode, tickClock, isActive);
        panels.Add(window);
        return window;
    }

    public static PanelFixture Create(bool configured = false)
    {
        return new PanelFixture(configured ? InMemoryCredentialStore.Configured() : new InMemoryCredentialStore());
    }

    public static async Task SettleAsync()
    {
        for (var round = 0; round < 10; round++)
        {
            await Task.Yield();
            Dispatcher.UIThread.RunJobs();
        }
    }

    public async Task ToSetupAsync(string? message = null)
    {
        if (message is null)
        {
            await Controller.BootstrapAsync();
            return;
        }

        Client.FailDevices(new ThinQException(ThinQErrorKind.InvalidToken));
        await Controller.ConnectAsync("tok", "BR");
    }

    public async Task ToLoadingWithMessageAsync()
    {
        Client.FailState(new ThinQException(ThinQErrorKind.Network));
        await Controller.BootstrapAsync();
    }

    public void ToLoadingHeld()
    {
        Client.HoldNextState();
        _ = Controller.BootstrapAsync();
    }

    public async Task ToChoosingAsync(bool switching = false)
    {
        if (switching)
        {
            await Controller.BootstrapAsync();
            await Controller.ChooseAnotherDeviceAsync();
            return;
        }

        await Controller.ConnectAsync("tok", "BR");
    }

    public async Task ToReadyAsync()
    {
        await Controller.BootstrapAsync();
    }

    public void Dispose()
    {
        foreach (var panel in panels)
        {
            try
            {
                panel.CloseForShutdown();
            }
            catch (InvalidOperationException)
            {
            }
        }

        Controller.StopPolling();
        try
        {
            Directory.Delete(directory, true);
        }
        catch (IOException)
        {
        }
    }
}
