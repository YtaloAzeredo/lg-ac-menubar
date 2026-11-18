using System.Diagnostics;
using System.Runtime.InteropServices;
using LGAC.App.Tray;

namespace LGAC.App.Tests;

[TestClass]
public sealed class ModeIconTests
{
    private const uint GdiObjects = 0;

    [TestMethod]
    public void RendersEveryModeAtRequestedSize()
    {
        foreach (var mode in new string?[] { "COOL", "HEAT", "AIR_DRY", "FAN", "AUTO", null })
        {
            foreach (var dimmed in new[] { false, true })
            {
                foreach (var light in new[] { false, true })
                {
                    foreach (var size in new[] { 16, 32 })
                    {
                        using var icon = ModeIcon.Render(mode, dimmed, light, size);

                        Assert.AreEqual(size, icon.Width);
                        Assert.AreEqual(size, icon.Height);
                    }
                }
            }
        }
    }

    [TestMethod]
    public void RenderDoesNotLeakHandles()
    {
        using var process = Process.GetCurrentProcess();
        var before = GetGuiResources(process.Handle, GdiObjects);

        for (var i = 0; i < 500; i++)
        {
            using var icon = ModeIcon.Render("COOL", false, false, 16);
        }

        var after = GetGuiResources(process.Handle, GdiObjects);

        Assert.IsLessThan(before + 20, after);
    }

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);
}
