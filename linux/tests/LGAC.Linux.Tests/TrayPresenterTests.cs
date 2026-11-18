using System.Text.Json.Nodes;
using Avalonia.Controls;
using LGAC.Core;
using LGAC.Core.Tests.Support;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Tray;

namespace LGAC.Linux.Tests;

[TestClass]
public class TrayPresenterTests
{
    private sealed record Rendered(string? Mode, bool Dimmed, int Size);

    private static TrayPresenter Create(PanelFixture fixture, FakeTrayHost host, List<Rendered> renders, Action? toggle = null, Action? show = null, Action? quit = null)
    {
        return new TrayPresenter(
            fixture.Controller,
            toggle ?? (() => { }),
            show ?? (() => { }),
            quit ?? (() => { }),
            host,
            (mode, dimmed, size) =>
            {
                renders.Add(new Rendered(mode, dimmed, size));
                return ModeIcon.Render(mode, dimmed, size);
            });
    }

    [TestMethod]
    public async Task StartsDimmedWithTheDefaultTip()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var fixture = PanelFixture.Create();
            var host = new FakeTrayHost();
            var renders = new List<Rendered>();
            using var presenter = Create(fixture, host, renders);

            CollectionAssert.AreEqual(new[] { "Ar-condicionado" }, host.ToolTips);
            Assert.HasCount(1, host.Icons);
            Assert.AreEqual(new Rendered(null, true, 44), renders[0]);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task MenuHasOpenPanelAndQuit()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var fixture = PanelFixture.Create();
            var host = new FakeTrayHost();
            var shown = 0;
            var quit = 0;
            using var presenter = Create(fixture, host, [], show: () => shown++, quit: () => quit++);

            CollectionAssert.AreEqual(new[] { "Abrir painel", "Sair" }, host.Menu.Select(entry => entry.Header).ToArray());
            host.Choose("Abrir painel");
            host.Choose("Sair");
            Assert.AreEqual(1, shown);
            Assert.AreEqual(1, quit);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task ClickTogglesThePanel()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var fixture = PanelFixture.Create();
            var host = new FakeTrayHost();
            var toggled = 0;
            using var presenter = Create(fixture, host, [], toggle: () => toggled++);

            host.Click();
            host.Click();
            Assert.AreEqual(2, toggled);
            return Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task UsesTheAliasAndTheModeWhenOn()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var host = new FakeTrayHost();
            var renders = new List<Rendered>();
            using var presenter = Create(fixture, host, renders);

            await fixture.ToReadyAsync();
            await PanelFixture.SettleAsync();

            Assert.AreEqual("Ar do Quarto", host.ToolTips[^1]);
            Assert.AreEqual(new Rendered("COOL", false, 44), renders[^1]);
        });
    }

    [TestMethod]
    public async Task DimsWhenTheUnitIsOff()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            await fixture.ToReadyAsync();
            var host = new FakeTrayHost();
            var renders = new List<Rendered>();
            using var presenter = Create(fixture, host, renders);
            Assert.IsFalse(renders[^1].Dimmed);

            var off = Fixture.Json("state-full");
            off!["operation"]!["airConOperationMode"] = JsonValue.Create("POWER_OFF");
            fixture.Client.SetState(off);
            fixture.Clock.Date = fixture.Clock.Date.AddMinutes(5);
            await fixture.Controller.RefreshAsync(force: true);
            await PanelFixture.SettleAsync();

            Assert.IsTrue(renders[^1].Dimmed);
        });
    }

    [TestMethod]
    public async Task BlankAliasFallsBackToTheDefaultTip()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            fixture.Store.DeviceAlias = "   ";
            var host = new FakeTrayHost();
            using var presenter = Create(fixture, host, []);
            await fixture.ToReadyAsync();
            await PanelFixture.SettleAsync();

            Assert.AreEqual("Ar-condicionado", host.ToolTips[^1]);
        });
    }

    [TestMethod]
    public async Task HostFailuresNeverEscape()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var host = new FakeTrayHost();
            using var presenter = Create(
                fixture,
                host,
                [],
                toggle: () => throw new InvalidOperationException("x"),
                show: () => throw new InvalidOperationException("x"),
                quit: () => throw new InvalidOperationException("x"));
            host.SetIconError = new InvalidOperationException("sem bandeja");

            host.Click();
            host.Choose("Abrir painel");
            host.Choose("Sair");
            await fixture.ToReadyAsync();
            await PanelFixture.SettleAsync();
        });
    }

    [TestMethod]
    public async Task DisposeStopsListeningAndDisposesTheHostOnce()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            using var fixture = PanelFixture.Create(configured: true);
            var host = new FakeTrayHost();
            var presenter = Create(fixture, host, []);
            var before = host.Icons.Count;

            presenter.Dispose();
            presenter.Dispose();
            await fixture.ToReadyAsync();
            await PanelFixture.SettleAsync();

            Assert.HasCount(before, host.Icons);
            Assert.AreEqual(1, host.DisposeCount);
        });
    }

    [TestMethod]
    public async Task ConstructorFailureDisposesTheHostAndRethrows()
    {
        await HeadlessApp.RunAsync(() =>
        {
            using var fixture = PanelFixture.Create();
            var host = new FakeTrayHost { SetMenuError = new InvalidOperationException("sem menu") };

            Assert.Throws<InvalidOperationException>(() => Create(fixture, host, []));
            Assert.AreEqual(1, host.DisposeCount);
            return Task.CompletedTask;
        });
    }
}
