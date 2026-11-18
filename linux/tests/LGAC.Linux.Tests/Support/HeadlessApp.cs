using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using LGAC.Linux;

namespace LGAC.Linux.Tests.Support;

public static class HeadlessApp
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(
        () => HeadlessUnitTestSession.StartNew(typeof(HeadlessEntry)));

    public static async Task RunAsync(Func<Task> body)
    {
        using var cancellation = new CancellationTokenSource(Timeout);
        var dispatched = Session.Value.Dispatch(
            async () =>
            {
                try
                {
                    await body();
                }
                finally
                {
                    UiTesting.CloseAll();
                }

                return true;
            },
            cancellation.Token);
        try
        {
            await dispatched.WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"O corpo do teste headless passou de {Timeout.TotalSeconds:0} s");
        }
    }

    public static string Capture(TopLevel window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("A janela não renderizou nenhum quadro");
        var directory = Path.Combine(AppContext.BaseDirectory, "captures");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".png");
        frame.Save(path, new PngBitmapEncoderOptions());
        return path;
    }
}

public static class HeadlessEntry
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .WithInterFont()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
