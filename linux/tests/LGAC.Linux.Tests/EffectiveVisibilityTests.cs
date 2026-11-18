using Avalonia.Controls;
using LGAC.Linux.Tests.Support;
using LGAC.Linux.Views;

namespace LGAC.Linux.Tests;

[TestClass]
public class EffectiveVisibilityTests
{
    [TestMethod]
    public async Task ReportsOnlyWhatIsReallyVisible()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var owner = new Border();
            var parent = new StackPanel { Children = { owner } };
            var changes = new List<bool>();
            using var watcher = new EffectiveVisibility(owner, changes.Add);
            Assert.IsFalse(watcher.IsVisible);

            var window = UiTesting.Host(parent);
            Assert.IsTrue(watcher.IsVisible);
            CollectionAssert.AreEqual(new[] { true }, changes);

            window.Hide();
            Assert.IsFalse(watcher.IsVisible);

            window.Show();
            Assert.IsTrue(watcher.IsVisible);

            parent.IsVisible = false;
            Assert.IsFalse(watcher.IsVisible);
            parent.IsVisible = true;
            owner.IsVisible = false;
            Assert.IsFalse(watcher.IsVisible);
            owner.IsVisible = true;
            Assert.IsTrue(watcher.IsVisible);

            window.Content = null;
            Assert.IsFalse(watcher.IsVisible);
            CollectionAssert.AreEqual(new[] { true, false, true, false, true, false, true, false }, changes);
            await Task.CompletedTask;
        });
    }

    [TestMethod]
    public async Task NotifiesWhenAttachedToAnAlreadyShownWindowAndStopsAfterDispose()
    {
        await HeadlessApp.RunAsync(async () =>
        {
            var window = UiTesting.Host(new Border());
            var owner = new Border();
            var changes = new List<bool>();
            var watcher = new EffectiveVisibility(owner, changes.Add);

            window.Content = owner;
            CollectionAssert.AreEqual(new[] { true }, changes);

            watcher.Dispose();
            CollectionAssert.AreEqual(new[] { true, false }, changes);
            window.Hide();
            window.Show();
            Assert.HasCount(2, changes);
            Assert.IsFalse(watcher.IsVisible);
            await Task.CompletedTask;
        });
    }
}
